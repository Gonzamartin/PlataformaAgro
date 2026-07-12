using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using CsvHelper;
using Serilog;

// Configurar logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/formAgro-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

// ARRANQUE DIRECTO DE LA APLICACIÓN (Sintaxis moderna sin llaves globales)
ApplicationConfiguration.Initialize();
Application.Run(new FormAgro());

/// <summary>
/// Modelo de datos para configuración de lotes agrícolas
/// </summary>
public class LoteConfig
{
    public string Nombre { get; set; } = string.Empty;
    public string Lat { get; set; } = string.Empty;
    public string Lon { get; set; } = string.Empty;

    public override string ToString() => Nombre;
}

/// <summary>
/// Modelos para respuesta de OpenWeatherMap API
/// </summary>
public class WeatherApiResponse
{
    [JsonPropertyName("main")]
    public MainWeatherData Main { get; set; } = new();

    [JsonPropertyName("wind")]
    public WindData Wind { get; set; } = new();

    [JsonPropertyName("sys")]
    public SystemData Sys { get; set; } = new();

    [JsonPropertyName("name")]
    public string CityName { get; set; } = string.Empty;
}

public class MainWeatherData
{
    [JsonPropertyName("temp")]
    public double Temp { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }
}

public class WindData
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }
}

public class SystemData
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;
}

/// <summary>
/// Modelo de respuesta de datos meteorológicos
/// </summary>
public class WeatherResponse
{
    public double Temperature { get; set; }
    public double WindSpeed { get; set; }
    public int Humidity { get; set; }
    public string Source { get; set; } = "API"; // "API" o "Simulado"
}

/// <summary>
/// Resultado de evaluación de condiciones de pulverización
/// </summary>
public class SprayConditionResult
{
    public const string SUITABLE = "APTO";
    public const string UNSUITABLE = "NO APTO";

    public string Condition { get; set; } = UNSUITABLE;
    public double Temperature { get; set; }
    public double WindSpeed { get; set; }
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Servicio de validación de datos
/// </summary>
public static class ValidationService
{
    public static bool TryParseCoordinate(string value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, 
            CultureInfo.InvariantCulture, out result);
    }

    public static bool IsValidLatitude(double lat) => lat >= -90 && lat <= 90;
    public static bool IsValidLongitude(double lon) => lon >= -180 && lon <= 180;
}

/// <summary>
/// Servicio de consulta meteorológica con OpenWeatherMap API
/// </summary>
public class WeatherService
{
    private const string OPENWEATHERMAP_URL = "https://api.openweathermap.org/data/2.5/weather";
    private const string API_KEY = "1d0b01e221bd26f2fee7a519c35ec2a4"; // Clave gratuita para pruebas
    private static readonly HttpClient httpClient = new();
    private static readonly Random rand = new Random();

    public async Task<WeatherResponse?> GetWeatherData(double latitude, double longitude, bool useRealApi = true)
    {
        if (useRealApi)
        {
            return await GetRealWeatherData(latitude, longitude);
        }
        else
        {
            return GetSimulatedWeatherData();
        }
    }

    private async Task<WeatherResponse?> GetRealWeatherData(double latitude, double longitude)
    {
        try
        {
            string url = $"{OPENWEATHERMAP_URL}?lat={latitude}&lon={longitude}&appid={API_KEY}&units=metric";
            
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var response = await httpClient.GetAsync(url, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var weatherData = JsonSerializer.Deserialize<WeatherApiResponse>(jsonContent, options);

                    if (weatherData != null)
                    {
                        Log.Information("Datos reales obtenidos de OpenWeatherMap - Temp: {Temp}°C, Viento: {Wind} km/h",
                            weatherData.Main.Temp, weatherData.Wind.Speed);

                        return new WeatherResponse
                        {
                            Temperature = weatherData.Main.Temp,
                            WindSpeed = weatherData.Wind.Speed * 3.6, // Convertir m/s a km/h
                            Humidity = weatherData.Main.Humidity,
                            Source = "API OpenWeatherMap"
                        };
                    }
                }
                else
                {
                    Log.Warning("Error en API OpenWeatherMap: {StatusCode}", response.StatusCode);
                }
            }
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            Log.Warning("Timeout al conectar con OpenWeatherMap. Usando datos simulados.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error al obtener datos meteorológicos reales");
        }

        // Fallback a datos simulados si falla la API
        return GetSimulatedWeatherData();
    }

    private WeatherResponse GetSimulatedWeatherData()
    {
        double temp = 16 + (29 - 16) * rand.NextDouble();
        double viento = 4 + (17 - 4) * rand.NextDouble();
        int humidity = rand.Next(30, 90);

        Log.Information("Usando datos simulados - Temp: {Temp}°C, Viento: {Wind} km/h", temp, viento);

        return new WeatherResponse
        {
            Temperature = temp,
            WindSpeed = viento,
            Humidity = humidity,
            Source = "Simulado (sin conexión a API)"
        };
    }
}

/// <summary>
/// Servicio de lógica de negocio para evaluación de condiciones de pulverización
/// </summary>
public class SprayEvaluationService
{
    private const double MIN_WIND = 5.0;
    private const double MAX_WIND = 15.0;
    private const double MAX_TEMP = 30.0;
    private const int MIN_HUMIDITY = 30;
    private const int MAX_HUMIDITY = 85;

    public SprayConditionResult EvaluateConditions(double temperature, double windSpeed, int humidity = 0)
    {
        var result = new SprayConditionResult
        {
            Temperature = temperature,
            WindSpeed = windSpeed
        };

        if (windSpeed < MIN_WIND)
        {
            result.Condition = SprayConditionResult.UNSUITABLE;
            result.Details = $"Viento muy bajo ({windSpeed:F1} km/h < {MIN_WIND} km/h)";
        }
        else if (windSpeed > MAX_WIND)
        {
            result.Condition = SprayConditionResult.UNSUITABLE;
            result.Details = $"Viento muy alto ({windSpeed:F1} km/h > {MAX_WIND} km/h)";
        }
        else if (temperature >= MAX_TEMP)
        {
            result.Condition = SprayConditionResult.UNSUITABLE;
            result.Details = $"Temperatura muy elevada ({temperature:F1}°C ≥ {MAX_TEMP}°C)";
        }
        else if (humidity > 0 && (humidity < MIN_HUMIDITY || humidity > MAX_HUMIDITY))
        {
            result.Condition = SprayConditionResult.UNSUITABLE;
            result.Details = $"Humedad fuera de rango ({humidity}% - óptimo: {MIN_HUMIDITY}-{MAX_HUMIDITY}%)";
        }
        else
        {
            result.Condition = SprayConditionResult.SUITABLE;
            result.Details = "Condiciones óptimas para pulverización";
        }

        return result;
    }
}

/// <summary>
/// Servicio para exportar datos a CSV
/// </summary>
public class CsvExportService
{
    private const string CSV_FILE = "historial_pulverizaciones.csv";
    private const string LOG_DIR = "logs";

    public bool ExportRecord(string nombreLote, string latitud, string longitud,
        string temperatura, string viento, string humedad, string condicion, string fuente, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            if (!Directory.Exists(LOG_DIR))
                Directory.CreateDirectory(LOG_DIR);

            bool isNewFile = !File.Exists(CSV_FILE);
            string fechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            using (var writer = new StreamWriter(CSV_FILE, true, System.Text.Encoding.UTF8))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                if (isNewFile)
                {
                    csv.WriteField("Fecha/Hora");
                    csv.WriteField("Lote");
                    csv.WriteField("Latitud");
                    csv.WriteField("Longitud");
                    csv.WriteField("Temperatura(C)");
                    csv.WriteField("Viento(km/h)");
                    csv.WriteField("Humedad(%)");
                    csv.WriteField("Condicion");
                    csv.WriteField("Fuente");
                    csv.NextRecord();
                }

                csv.WriteField(fechaHora);
                csv.WriteField(nombreLote);
                csv.WriteField(latitud);
                csv.WriteField(longitud);
                csv.WriteField(temperatura);
                csv.WriteField(viento);
                csv.WriteField(humedad);
                csv.WriteField(condicion);
                csv.WriteField(fuente);
                csv.NextRecord();
            }

            Log.Information("Registro exportado correctamente: {Lote} - {Condicion} - {Fuente}", nombreLote, condicion, fuente);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            Log.Error(ex, "Error al exportar registro a CSV");
            return false;
        }
    }
}

/// <summary>
/// Formulario principal de la aplicación AgTech
/// </summary>
public class FormAgro : Form
{
    // Componentes UI
    private ComboBox cmbLotes = new ComboBox();
    private TextBox txtLatitud = new TextBox();
    private TextBox txtLongitud = new TextBox();
    private Button btnConsultar = new Button();
    private Button btnExportar = new Button();
    private CheckBox chkUsarAPI = new CheckBox();

    private Label lblTempValor = new Label();
    private Label lblVientoValor = new Label();
    private Label lblHumedadValor = new Label();
    private Label lblPulvValor = new Label();
    private Label lblEstadoConexion = new Label();
    private Label lblFuenteDatos = new Label();
    private ProgressBar pbProgreso = new ProgressBar();

    // Estado actual
    private string tempActual = "--";
    private string vientoActual = "--";
    private string humedadActual = "--";
    private string condicionActual = "SIN DATOS";
    private string fuenteDatos = "--";

    // Servicios
    private readonly WeatherService _weatherService = new WeatherService();
    private readonly SprayEvaluationService _sprayService = new SprayEvaluationService();
    private readonly CsvExportService _csvService = new CsvExportService();

    public FormAgro()
    {
        this.Text = "Plataforma AgTech - Control Operativo (v1.0.1)";
        this.Size = new Size(520, 720);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(242, 246, 242);

        InitializeComponents();
        LoadLotes();

        Log.Information("Aplicación FormAgro iniciada - Versión 1.0.1");
    }

    private void InitializeComponents()
    {
        // Título
        Label lblTitulo = new Label()
        {
            Text = "Auditoría Climática de Lotes",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(25, 15),
            Size = new Size(450, 35),
            ForeColor = Color.FromArgb(34, 112, 63)
        };
        this.Controls.Add(lblTitulo);

        // ComboBox de lotes
        Label lblCombo = new Label()
        {
            Text = "Seleccionar Lote:",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Location = new Point(30, 65),
            Size = new Size(110, 25)
        };
        cmbLotes = new ComboBox()
        {
            Location = new Point(150, 62),
            Size = new Size(310, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9)
        };
        cmbLotes.SelectedIndexChanged += CmbLotes_SelectedIndexChanged;
        this.Controls.Add(lblCombo);
        this.Controls.Add(cmbLotes);

        // Latitud
        Label lblLat = new Label()
        {
            Text = "Latitud:",
            Font = new Font("Segoe UI", 9),
            Location = new Point(30, 110),
            Size = new Size(70, 25)
        };
        txtLatitud = new TextBox()
        {
            Location = new Point(110, 107),
            Size = new Size(110, 25),
            Font = new Font("Segoe UI", 9),
            Text = "-34.12"
        };
        this.Controls.Add(lblLat);
        this.Controls.Add(txtLatitud);

        // Longitud
        Label lblLon = new Label()
        {
            Text = "Longitud:",
            Font = new Font("Segoe UI", 9),
            Location = new Point(250, 110),
            Size = new Size(70, 25)
        };
        txtLongitud = new TextBox()
        {
            Location = new Point(330, 107),
            Size = new Size(110, 25),
            Font = new Font("Segoe UI", 9),
            Text = "-60.57"
        };
        this.Controls.Add(lblLon);
        this.Controls.Add(txtLongitud);

        // CheckBox Usar API
        chkUsarAPI = new CheckBox()
        {
            Text = "Usar API Real",
            Location = new Point(30, 138),
            Size = new Size(150, 20),
            Font = new Font("Segoe UI", 9),
            Checked = true,
            ForeColor = Color.FromArgb(34, 112, 63),
            AutoCheck = true
        };
        this.Controls.Add(chkUsarAPI);

        // Botón Consultar
        btnConsultar = new Button()
        {
            Text = "Consultar Clima en Lote",
            Location = new Point(30, 170),
            Size = new Size(430, 40),
            BackColor = Color.FromArgb(34, 112, 63),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        btnConsultar.Click += BtnConsultar_Click;
        this.Controls.Add(btnConsultar);

        // Barra de progreso
        pbProgreso = new ProgressBar()
        {
            Location = new Point(30, 215),
            Size = new Size(430, 10),
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };
        this.Controls.Add(pbProgreso);

        // GroupBox de resultados
        GroupBox grpResultados = new GroupBox()
        {
            Text = " INDICADORES OPERATIVOS EN LOTE ",
            Location = new Point(25, 235),
            Size = new Size(440, 310),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.DimGray
        };
        this.Controls.Add(grpResultados);

        // Temperatura
        Label lblTempTitulo = new Label()
        {
            Text = "🌡️ Temperatura Aire:",
            Location = new Point(20, 40),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = Color.Black
        };
        lblTempValor = new Label()
        {
            Text = "-- °C",
            Location = new Point(220, 40),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.DarkSlateGray
        };
        grpResultados.Controls.Add(lblTempTitulo);
        grpResultados.Controls.Add(lblTempValor);

        // Viento
        Label lblVientoTitulo = new Label()
        {
            Text = "💨 Velocidad Viento:",
            Location = new Point(20, 90),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = Color.Black
        };
        lblVientoValor = new Label()
        {
            Text = "-- km/h",
            Location = new Point(220, 90),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.DarkSlateGray
        };
        grpResultados.Controls.Add(lblVientoTitulo);
        grpResultados.Controls.Add(lblVientoValor);

        // Humedad
        Label lblHumedadTitulo = new Label()
        {
            Text = "💧 Humedad Relativa:",
            Location = new Point(20, 140),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = Color.Black
        };
        lblHumedadValor = new Label()
        {
            Text = "-- %",
            Location = new Point(220, 140),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.DarkSlateGray
        };
        grpResultados.Controls.Add(lblHumedadTitulo);
        grpResultados.Controls.Add(lblHumedadValor);

        // Condición de pulverización
        Label lblPulvTitulo = new Label()
        {
            Text = "🚜 Aplicación Terrestre:",
            Location = new Point(20, 190),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.Black
        };
        lblPulvValor = new Label()
        {
            Text = "ESPERANDO DATOS",
            Location = new Point(220, 185),
            Size = new Size(190, 50),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Black
        };
        grpResultados.Controls.Add(lblPulvTitulo);
        grpResultados.Controls.Add(lblPulvValor);

        // Fuente de datos
        Label lblFuenteTitulo = new Label()
        {
            Text = "📊 Fuente de Datos:",
            Location = new Point(20, 250),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.DimGray
        };
        lblFuenteDatos = new Label()
        {
            Text = "--",
            Location = new Point(220, 250),
            Size = new Size(190, 25),
            Font = new Font("Segoe UI", 9, FontStyle.Italic),
            ForeColor = Color.DimGray
        };
        grpResultados.Controls.Add(lblFuenteTitulo);
        grpResultados.Controls.Add(lblFuenteDatos);

        // Botón Exportar
        btnExportar = new Button()
        {
            Text = "💾 Guardar Registro en Historial (CSV)",
            Location = new Point(30, 560),
            Size = new Size(430, 35),
            BackColor = Color.FromArgb(70, 80, 70),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        btnExportar.Click += BtnExportar_Click;
        this.Controls.Add(btnExportar);

        // Estado de conexión
        lblEstadoConexion = new Label()
        {
            Text = "Sistema listo. Seleccione un lote.",
            Location = new Point(25, 610),
            Size = new Size(450, 70),
            Font = new Font("Segoe UI", 8, FontStyle.Italic),
            ForeColor = Color.Gray,
            AutoSize = false
        };
        this.Controls.Add(lblEstadoConexion);
    }

    private void LoadLotes()
    {
        cmbLotes.Items.Add(new LoteConfig { Nombre = "[Ingreso Manual]", Lat = "", Lon = "" });
        cmbLotes.Items.Add(new LoteConfig
        {
            Nombre = "Lote Norte (Establecimiento El Ombú)",
            Lat = "-34.1200",
            Lon = "-60.5700"
        });
        cmbLotes.Items.Add(new LoteConfig
        {
            Nombre = "Lote Bajo Grande (Trigo / Maíz)",
            Lat = "-31.4200",
            Lon = "-64.1800"
        });
        cmbLotes.Items.Add(new LoteConfig
        {
            Nombre = "Lote Campo Escuela (UNC-FCA)",
            Lat = "-31.4798",
            Lon = "-64.0028"
        });
        cmbLotes.SelectedIndex = 0;
    }

    private void CmbLotes_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbLotes.SelectedItem is LoteConfig lote && lote.Nombre != "[Ingreso Manual]")
        {
            txtLatitud.Text = lote.Lat;
            txtLongitud.Text = lote.Lon;
        }
    }

    private async void BtnConsultar_Click(object? sender, EventArgs e)
    {
        // Validar coordenadas
        if (!ValidationService.TryParseCoordinate(txtLatitud.Text, out double lat))
        {
            MessageBox.Show("Latitud inválida. Use formato numérico (ej: -34.12)",
                "Error de Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Log.Warning("Latitud inválida ingresada: {Input}", txtLatitud.Text);
            return;
        }

        if (!ValidationService.TryParseCoordinate(txtLongitud.Text, out double lon))
        {
            MessageBox.Show("Longitud inválida. Use formato numérico (ej: -60.57)",
                "Error de Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Log.Warning("Longitud inválida ingresada: {Input}", txtLongitud.Text);
            return;
        }

        if (!ValidationService.IsValidLatitude(lat) || !ValidationService.IsValidLongitude(lon))
        {
            MessageBox.Show("Coordenadas fuera de rango válido.\nLatitud: [-90, 90]\nLongitud: [-180, 180]",
                "Error de Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Log.Warning("Coordenadas fuera de rango: Lat={Lat}, Lon={Lon}", lat, lon);
            return;
        }

        // Desactivar controles
        btnConsultar.Enabled = false;
        btnExportar.Enabled = false;
        pbProgreso.Visible = true;

        lblEstadoConexion.Text = chkUsarAPI.Checked 
            ? "📡 Sincronizando con OpenWeatherMap API..." 
            : "🔄 Generando datos simulados...";
        lblPulvValor.Text = "PROCESANDO";
        lblPulvValor.BackColor = Color.LightGoldenrodYellow;
        lblPulvValor.ForeColor = Color.DarkGoldenrod;

        try
        {
            // Obtener datos meteorológicos (real o simulado)
            var weatherData = await _weatherService.GetWeatherData(lat, lon, chkUsarAPI.Checked);

            if (weatherData == null)
            {
                throw new Exception("No se pudieron obtener datos meteorológicos");
            }

            tempActual = weatherData.Temperature.ToString("0.0", CultureInfo.InvariantCulture);
            vientoActual = weatherData.WindSpeed.ToString("0.0", CultureInfo.InvariantCulture);
            humedadActual = weatherData.Humidity.ToString();
            fuenteDatos = weatherData.Source;

            // Evaluar condiciones
            var result = _sprayService.EvaluateConditions(
                weatherData.Temperature, 
                weatherData.WindSpeed, 
                weatherData.Humidity);
            condicionActual = result.Condition;

            // Actualizar UI
            lblTempValor.Text = tempActual + " °C";
            lblVientoValor.Text = vientoActual + " km/h";
            lblHumedadValor.Text = humedadActual + " %";
            lblPulvValor.Text = condicionActual + "\n" + result.Details;
            lblFuenteDatos.Text = fuenteDatos;

            if (condicionActual == SprayConditionResult.SUITABLE)
            {
                lblPulvValor.BackColor = Color.FromArgb(220, 245, 220);
                lblPulvValor.ForeColor = Color.DarkGreen;
            }
            else
            {
                lblPulvValor.BackColor = Color.FromArgb(255, 220, 220);
                lblPulvValor.ForeColor = Color.DarkRed;
            }

            lblEstadoConexion.Text = "✅ Reporte actualizado con éxito.\nCoordenadas: " + lat + ", " + lon + "\nFuente: " + fuenteDatos;
            btnExportar.Enabled = true;

            Log.Information("Consulta exitosa - Temp: {Temp}°C, Viento: {Viento} km/h, Humedad: {Humidity}%, Fuente: {Source}",
                tempActual, vientoActual, humedadActual, fuenteDatos);
        }
        catch (Exception ex)
        {
            lblEstadoConexion.Text = "❌ Error: " + ex.Message + "\nIntentando modo fallback...";
            MessageBox.Show("Error durante la consulta: " + ex.Message, "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log.Error(ex, "Error durante la consulta meteorológica");
        }
        finally
        {
            btnConsultar.Enabled = true;
            pbProgreso.Visible = false;
        }
    }

    private void BtnExportar_Click(object? sender, EventArgs e)
    {
        try
        {
            string nombreLote = cmbLotes.SelectedItem is LoteConfig lote ? lote.Nombre : "Lote Manual";

            if (_csvService.ExportRecord(nombreLote, txtLatitud.Text, txtLongitud.Text,
                tempActual, vientoActual, humedadActual, condicionActual, fuenteDatos, out string errorMessage))
            {
                lblEstadoConexion.Text = "💾 Registro guardado con éxito.";
                MessageBox.Show("¡Éxito! Auditoría operativa guardada correctamente.",
                    "Registro Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Error al escribir el archivo: " + errorMessage,
                    "Error de Almacenamiento", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error inesperado: " + ex.Message, "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log.Error(ex, "Error al exportar registro");
        }
    }
}
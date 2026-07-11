using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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
/// Modelo de respuesta de datos meteorológicos
/// </summary>
public class WeatherResponse
{
    public double Temperature { get; set; }
    public double WindSpeed { get; set; }
    public double Humidity { get; set; }
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
/// Servicio de lógica de negocio para evaluación de condiciones de pulverización
/// </summary>
public class SprayEvaluationService
{
    private const double MIN_WIND = 5.0;
    private const double MAX_WIND = 15.0;
    private const double MAX_TEMP = 30.0;

    public SprayConditionResult EvaluateConditions(double temperature, double windSpeed)
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
        string temperatura, string viento, string condicion, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            // Crear directorio de logs si no existe
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
                    csv.WriteField("Condicion");
                    csv.NextRecord();
                }

                csv.WriteField(fechaHora);
                csv.WriteField(nombreLote);
                csv.WriteField(latitud);
                csv.WriteField(longitud);
                csv.WriteField(temperatura);
                csv.WriteField(viento);
                csv.WriteField(condicion);
                csv.NextRecord();
            }

            Log.Information("Registro exportado correctamente: {Lote} - {Condicion}", nombreLote, condicion);
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

    private Label lblTempValor = new Label();
    private Label lblVientoValor = new Label();
    private Label lblPulvValor = new Label();
    private Label lblEstadoConexion = new Label();
    private ProgressBar pbProgreso = new ProgressBar();

    // Estado actual
    private string tempActual = "--";
    private string vientoActual = "--";
    private string condicionActual = "SIN DATOS";

    // Servicios
    private readonly SprayEvaluationService _sprayService = new SprayEvaluationService();
    private readonly CsvExportService _csvService = new CsvExportService();
    private static readonly Random rand = new Random();

    public FormAgro()
    {
        this.Text = "Plataforma AgTech - Control Operativo";
        this.Size = new Size(520, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(242, 246, 242);

        InitializeComponents();
        LoadLotes();

        Log.Information("Aplicación FormAgro iniciada");
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

        // Botón Consultar
        btnConsultar = new Button()
        {
            Text = "Consultar Clima en Lote",
            Location = new Point(30, 155),
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
            Location = new Point(30, 200),
            Size = new Size(430, 10),
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };
        this.Controls.Add(pbProgreso);

        // GroupBox de resultados
        GroupBox grpResultados = new GroupBox()
        {
            Text = " INDICADORES OPERATIVOS EN LOTE ",
            Location = new Point(25, 220),
            Size = new Size(440, 260),
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
            Location = new Point(20, 95),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = Color.Black
        };
        lblVientoValor = new Label()
        {
            Text = "-- km/h",
            Location = new Point(220, 95),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.DarkSlateGray
        };
        grpResultados.Controls.Add(lblVientoTitulo);
        grpResultados.Controls.Add(lblVientoValor);

        // Condición de pulverización
        Label lblPulvTitulo = new Label()
        {
            Text = "🚜 Aplicación Terrestre:",
            Location = new Point(20, 160),
            Size = new Size(180, 25),
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.Black
        };
        lblPulvValor = new Label()
        {
            Text = "ESPERANDO DATOS",
            Location = new Point(220, 155),
            Size = new Size(190, 35),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Black
        };
        grpResultados.Controls.Add(lblPulvTitulo);
        grpResultados.Controls.Add(lblPulvValor);

        // Botón Exportar
        btnExportar = new Button()
        {
            Text = "💾 Guardar Registro en Historial (CSV)",
            Location = new Point(30, 500),
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
            Location = new Point(25, 555),
            Size = new Size(450, 50),
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

        lblEstadoConexion.Text = "📡 Sincronizando variables con la red local AgTech...";
        lblPulvValor.Text = "PROCESANDO";
        lblPulvValor.BackColor = Color.LightGoldenrodYellow;
        lblPulvValor.ForeColor = Color.DarkGoldenrod;

        try
        {
            // Simular consulta meteorológica (reemplazar por API real)
            await System.Threading.Tasks.Task.Delay(1500);

            double temp = 16 + (29 - 16) * rand.NextDouble();
            double viento = 4 + (17 - 4) * rand.NextDouble();

            tempActual = temp.ToString("0.0", CultureInfo.InvariantCulture);
            vientoActual = viento.ToString("0.0", CultureInfo.InvariantCulture);

            // Evaluar condiciones
            var result = _sprayService.EvaluateConditions(temp, viento);
            condicionActual = result.Condition;

            // Actualizar UI
            lblTempValor.Text = tempActual + " °C";
            lblVientoValor.Text = vientoActual + " km/h";
            lblPulvValor.Text = condicionActual + "\n" + result.Details;

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

            lblEstadoConexion.Text = "✅ Reporte actualizado con éxito. Coordenadas: " + lat + ", " + lon;
            btnExportar.Enabled = true;

            Log.Information("Consulta exitosa - Temp: {Temp}°C, Viento: {Viento} km/h, Condición: {Condicion}",
                temp, viento, condicionActual);
        }
        catch (Exception ex)
        {
            lblEstadoConexion.Text = "❌ Falla interna: " + ex.Message;
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
                tempActual, vientoActual, condicionActual, out string errorMessage))
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

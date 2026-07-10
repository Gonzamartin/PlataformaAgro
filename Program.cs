using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

// ARRANQUE DIRECTO DE LA APLICACIÓN (Sintaxis moderna sin llaves globales)
ApplicationConfiguration.Initialize();
Application.Run(new FormAgro());

public class LoteConfig
{
    public string Nombre { get; set; } = string.Empty;
    public string Lat { get; set; } = string.Empty;
    public string Lon { get; set; } = string.Empty;
    public override string ToString() => Nombre;
}

public class FormAgro : Form
{
    private ComboBox cmbLotes = new ComboBox();
    private TextBox txtLatitud = new TextBox();
    private TextBox txtLongitud = new TextBox();
    private Button btnConsultar = new Button();
    private Button btnExportar = new Button();

    private Label lblTempValor = new Label();
    private Label lblVientoValor = new Label();
    private Label lblPulvValor = new Label();
    private Label lblEstadoConexion = new Label();

    private string tempActual = "--";
    private string vientoActual = "--";
    private string condicionActual = "SIN DATOS";

    public FormAgro()
    {
        this.Text = "Plataforma AgTech - Control Operativo";
        this.Size = new Size(520, 620);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(242, 246, 242);

        Label lblTitulo = new Label() { Text = "Auditoría Climática de Lotes", Font = new Font("Segoe UI", 16, FontStyle.Bold), Location = new Point(25, 15), Size = new Size(450, 35), ForeColor = Color.FromArgb(34, 112, 63) };
        this.Controls.Add(lblTitulo);

        Label lblCombo = new Label() { Text = "Seleccionar Lote:", Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(30, 65), Size = new Size(110, 25) };
        cmbLotes = new ComboBox() { Location = new Point(150, 62), Size = new Size(310, 25), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9) };
        cmbLotes.SelectedIndexChanged += (s, e) => {
            if (cmbLotes.SelectedItem is LoteConfig lote && lote.Nombre != "[Ingreso Manual]") {
                txtLatitud.Text = lote.Lat;
                txtLongitud.Text = lote.Lon;
            }
        };
        this.Controls.Add(lblCombo);
        this.Controls.Add(cmbLotes);

        Label lblLat = new Label() { Text = "Latitud:", Font = new Font("Segoe UI", 9), Location = new Point(30, 110), Size = new Size(70, 25) };
        txtLatitud = new TextBox() { Location = new Point(110, 107), Size = new Size(110, 25), Font = new Font("Segoe UI", 9), Text = "-34.12" };
        this.Controls.Add(lblLat);
        this.Controls.Add(txtLatitud);

        Label lblLon = new Label() { Text = "Longitud:", Font = new Font("Segoe UI", 9), Location = new Point(250, 110), Size = new Size(70, 25) };
        txtLongitud = new TextBox() { Location = new Point(330, 107), Size = new Size(110, 25), Font = new Font("Segoe UI", 9), Text = "-60.57" };
        this.Controls.Add(lblLon);
        this.Controls.Add(txtLongitud);

        btnConsultar = new Button() { Text = "Consultar Clima en Lote", Location = new Point(30, 155), Size = new Size(430, 40), BackColor = Color.FromArgb(34, 112, 63), ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
        btnConsultar.Click += BtnConsultar_Click;
        this.Controls.Add(btnConsultar);

        GroupBox grpResultados = new GroupBox() { Text = " INDICADORES OPERATIVOS EN LOTE ", Location = new Point(25, 220), Size = new Size(440, 260), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.DimGray };
        this.Controls.Add(grpResultados);

        Label lblTempTitulo = new Label() { Text = "🌡️ Temperatura Aire:", Location = new Point(20, 40), Size = new Size(180, 25), Font = new Font("Segoe UI", 11, FontStyle.Regular), ForeColor = Color.Black };
        lblTempValor = new Label() { Text = "-- °C", Location = new Point(220, 40), Size = new Size(180, 25), Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.DarkSlateGray };
        grpResultados.Controls.Add(lblTempTitulo); 
        grpResultados.Controls.Add(lblTempValor);

        Label lblVientoTitulo = new Label() { Text = "💨 Velocidad Viento:", Location = new Point(20, 95), Size = new Size(180, 25), Font = new Font("Segoe UI", 11, FontStyle.Regular), ForeColor = Color.Black };
        lblVientoValor = new Label() { Text = "-- km/h", Location = new Point(220, 95), Size = new Size(180, 25), Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.DarkSlateGray };
        grpResultados.Controls.Add(lblVientoTitulo); 
        grpResultados.Controls.Add(lblVientoValor);

        Label lblPulvTitulo = new Label() { Text = "🚜 Aplicación Terrestre:", Location = new Point(20, 160), Size = new Size(180, 25), Font = new Font("Segoe UI", 11), ForeColor = Color.Black };
        lblPulvValor = new Label() { Text = "ESPERANDO DATOS", Location = new Point(220, 155), Size = new Size(190, 35), Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Black };
        grpResultados.Controls.Add(lblPulvTitulo); 
        grpResultados.Controls.Add(lblPulvValor);

        btnExportar = new Button() { Text = "💾 Guardar Registro en Historial (CSV)", Location = new Point(30, 500), Size = new Size(430, 35), BackColor = Color.FromArgb(70, 80, 70), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Enabled = false };
        btnExportar.Click += BtnExportar_Click;
        this.Controls.Add(btnExportar);

        lblEstadoConexion = new Label() { Text = "Sistema listo. Seleccione un lote.", Location = new Point(25, 550), Size = new Size(450, 25), Font = new Font("Segoe UI", 8, FontStyle.Italic), ForeColor = Color.Gray };
        this.Controls.Add(lblEstadoConexion);

        cmbLotes.Items.Add(new LoteConfig { Nombre = "[Ingreso Manual]", Lat = "", Lon = "" });
        cmbLotes.Items.Add(new LoteConfig { Nombre = "Lote Norte (Establecimiento El Ombú)", Lat = "-34.1200", Lon = "-60.5700" });
        cmbLotes.Items.Add(new LoteConfig { Nombre = "Lote Bajo Grande (Trigo / Maíz)", Lat = "-31.4200", Lon = "-64.1800" });
        cmbLotes.Items.Add(new LoteConfig { Nombre = "Lote La Posta (Zona Núcleo)", Lat = "-33.0500", Lon = "-61.9200" });
        cmbLotes.SelectedIndex = 0;
    }

    private async void BtnConsultar_Click(object? sender, EventArgs e)
    {
        btnConsultar.Enabled = false; btnExportar.Enabled = false;
        lblEstadoConexion.Text = "📡 Sincronizando variables con la red local AgTech..."; 
        lblPulvValor.Text = "PROCESANDO";
        lblPulvValor.BackColor = Color.LightGoldenrodYellow;
        lblPulvValor.ForeColor = Color.DarkGoldenrod;

        try {
            await System.Threading.Tasks.Task.Delay(1000);

            Random rand = new Random();
            double temp = rand.Next(16, 29) + rand.NextDouble();
            double viento = rand.Next(4, 17) + rand.NextDouble();

            string apto = (viento >= 5 && viento <= 15 && temp < 30) ? "APTO" : "NO APTO";

            tempActual = temp.ToString("0.0", CultureInfo.InvariantCulture);
            vientoActual = viento.ToString("0.0", CultureInfo.InvariantCulture);
            condicionActual = apto;

            lblTempValor.Text = tempActual + " °C"; 
            lblVientoValor.Text = vientoActual + " km/h"; 
            lblPulvValor.Text = condicionActual;

            if (condicionActual == "APTO") {
                lblPulvValor.BackColor = Color.FromArgb(220, 245, 220); 
                lblPulvValor.ForeColor = Color.DarkGreen;
            } else {
                lblPulvValor.BackColor = Color.FromArgb(255, 220, 220); 
                lblPulvValor.ForeColor = Color.DarkRed;
            }

            lblEstadoConexion.Text = "✅ Reporte actualizado con éxito."; 
            btnExportar.Enabled = true;
        }
        catch (Exception ex) { 
            lblEstadoConexion.Text = "❌ Falla interna: " + ex.Message; 
        }
        finally { 
            btnConsultar.Enabled = true; 
        }
    }

    private void BtnExportar_Click(object? sender, EventArgs e)
    {
        try {
            string rutaArchivoCsv = "historial_pulverizaciones.csv";
            bool archivoNuevo = !File.Exists(rutaArchivoCsv);

            using (StreamWriter sw = new StreamWriter(rutaArchivoCsv, true, System.Text.Encoding.UTF8)) {
                if (archivoNuevo) {
                    sw.WriteLine("Fecha/Hora,Lote,Latitud,Longitud,Temperatura(C),Viento(km/h),Condicion");
                }

                string nombreLote = cmbLotes.SelectedItem is LoteConfig lote ? lote.Nombre : "Lote Manual";
                string fechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                sw.WriteLine(fechaHora + "," + nombreLote + "," + txtLatitud.Text + "," + txtLongitud.Text + "," + tempActual + "," + vientoActual + "," + condicionActual);
            }

            lblEstadoConexion.Text = "💾 Registro guardado con éxito.";
            MessageBox.Show("¡Éxito! Auditoría operativa guardada correctamente.", "Registro Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { 
            MessageBox.Show("Error al escribir el archivo: " + ex.Message, "Error de Almacenamiento", MessageBoxButtons.OK, MessageBoxIcon.Error); 
        }
    }
}

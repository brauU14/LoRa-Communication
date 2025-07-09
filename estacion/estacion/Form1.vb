' Imports statements should be at the very top of the file
Imports System.IO.Ports
Imports System.Windows.Forms

Public Class MainForm
    Inherits System.Windows.Forms.Form

#Region "Control Declarations"
    ' Controls are declared here to be accessible throughout the form class.
    Friend WithEvents GroupBoxEnvironmentalData As GroupBox
    Friend WithEvents LabelTemperature As Label
    Friend WithEvents LabelHumidity As Label
    Friend WithEvents LabelTempHeader As Label
    Friend WithEvents LabelHumidHeader As Label
    Friend WithEvents PanelHumidityIndicator As Panel
    Friend WithEvents GroupBoxSystemStatus As GroupBox
    Friend WithEvents LabelPumpStatus As Label
    Friend WithEvents LabelPumpStatusHeader As Label
    Friend WithEvents GroupBoxManualControl As GroupBox
    Friend WithEvents RadioButtonManual As RadioButton
    Friend WithEvents RadioButtonAutomatic As RadioButton
    Friend WithEvents PanelManualControl As Panel
    Friend WithEvents ButtonTurnOnPump As Button
    Friend WithEvents ButtonTurnOffPump As Button
    Friend WithEvents GroupBoxSerialConnection As GroupBox
    Friend WithEvents ComboBoxPorts As ComboBox
    Friend WithEvents ButtonConnect As Button
    Friend WithEvents ButtonDisconnect As Button
    Friend WithEvents LabelConnectionStatus As Label
    Friend WithEvents RichTextBoxSerialData As RichTextBox
    Friend WithEvents LabelLastUpdate As Label
    Friend WithEvents LabelDeveloper As Label
#End Region

    ' Declare the SerialPort object
    Private WithEvents serialPort As New SerialPort()

    ' Delegate to update UI from a different thread
    Private Delegate Sub UpdateUiDelegate(text As String)
    Private uiUpdater As UpdateUiDelegate

    Public Sub New()
        ' This call is required by the designer.
        InitializeComponent()

        ' Initialize the delegate
        uiUpdater = New UpdateUiDelegate(AddressOf UpdateUI)
    End Sub

    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Set the title of the form
        Me.Text = "Estación de Monitoreo y Riego Automatizado - UTSLRC"

        ' Populate the ComboBox with available COM ports
        For Each port As String In SerialPort.GetPortNames()
            ComboBoxPorts.Items.Add(port)
        Next

        ' If there are available ports, select the first one
        If ComboBoxPorts.Items.Count > 0 Then
            ComboBoxPorts.SelectedIndex = 0
        Else
            ' Inform the user if no COM ports are found
            MessageBox.Show("No se encontraron puertos COM. Asegúrese de que el dispositivo esté conectado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ButtonConnect.Enabled = False
        End If

        ' Initial state for buttons
        ButtonDisconnect.Enabled = False
        PanelManualControl.Enabled = False ' Disable manual control initially
        RadioButtonAutomatic.Checked = True ' Default to Automatic mode
    End Sub

    Private Sub ButtonConnect_Click(sender As Object, e As EventArgs) Handles ButtonConnect.Click
        Try
            ' Configure and open the serial port
            If ComboBoxPorts.SelectedItem IsNot Nothing Then
                serialPort.PortName = ComboBoxPorts.SelectedItem.ToString()
                serialPort.BaudRate = 115200 ' Match this with your Arduino's baud rate
                serialPort.Open()

                ' Update UI to reflect connection status
                ButtonConnect.Enabled = False
                ButtonDisconnect.Enabled = True
                ComboBoxPorts.Enabled = False
                LabelConnectionStatus.Text = "Estado: Conectado"
                LabelConnectionStatus.ForeColor = Color.Green
            Else
                MessageBox.Show("Por favor, seleccione un puerto COM.", "Puerto no seleccionado", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        Catch ex As Exception
            ' Handle exceptions during connection
            MessageBox.Show("Error al conectar: " & ex.Message, "Error de Conexión", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ButtonDisconnect_Click(sender As Object, e As EventArgs) Handles ButtonDisconnect.Click
        Try
            ' Close the serial port if it is open
            If serialPort.IsOpen Then
                serialPort.Close()
            End If

            ' Update UI to reflect disconnection status
            ButtonConnect.Enabled = True
            ButtonDisconnect.Enabled = False
            ComboBoxPorts.Enabled = True
            LabelConnectionStatus.Text = "Estado: No conectado"
            LabelConnectionStatus.ForeColor = Color.Red

            ' Reset data labels
            LabelTemperature.Text = "N/A"
            LabelHumidity.Text = "N/A"
            LabelPumpStatus.Text = "DESCONOCIDO"
            PanelHumidityIndicator.BackColor = SystemColors.Control
            RichTextBoxSerialData.Clear()

        Catch ex As Exception
            ' Handle exceptions during disconnection
            MessageBox.Show("Error al desconectar: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub serialPort_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles serialPort.DataReceived
        ' This event is raised on a different thread, so we need to use a delegate
        ' to update the UI safely.
        Try
            Dim receivedData As String = serialPort.ReadLine()
            Me.Invoke(uiUpdater, receivedData)
        Catch ex As Exception
            ' Ignore timeout exceptions which can occur when closing the port
            If Not TypeOf ex Is TimeoutException Then
                ' You might want to log other exceptions here
            End If
        End Try
    End Sub

    Private Sub UpdateUI(ByVal data As String)
        ' This method is called by the delegate to update the UI
        ' It parses the data and updates the controls

        ' Append raw data to the RichTextBox
        RichTextBoxSerialData.AppendText(data & vbCrLf)

        ' Example data format from Arduino: "T:29.1,H:315,P:1"
        ' T = Temperature, H = Humidity, P = Pump State (1 for ON, 0 for OFF)
        data = data.Trim()
        Dim parts As String() = data.Split(","c)

        For Each part As String In parts
            Dim pair As String() = part.Split(":"c)
            If pair.Length = 2 Then
                Dim key As String = pair(0).Trim()
                Dim value As String = pair(1).Trim()

                Select Case key
                    Case "T" ' Temperature
                        LabelTemperature.Text = value & " °C"
                    Case "H" ' Humidity
                        LabelHumidity.Text = value
                        Try
                            Dim humidityValue As Integer = CInt(value)
                            ' Change color of the panel based on humidity
                            ' Assuming "dry" is a value > 500 and "wet" is <= 500
                            If humidityValue > 500 Then
                                PanelHumidityIndicator.BackColor = Color.Brown ' Dry
                            Else
                                PanelHumidityIndicator.BackColor = Color.DodgerBlue ' Wet
                            End If
                        Catch ex As FormatException
                            ' Handle case where humidity value is not a valid integer
                            PanelHumidityIndicator.BackColor = Color.Gray ' Unknown state
                        End Try
                    Case "P" ' Pump State
                        If value = "1" Then
                            LabelPumpStatus.Text = "ENCENDIDA"
                            LabelPumpStatus.ForeColor = Color.Green
                        Else
                            LabelPumpStatus.Text = "APAGADA"
                            LabelPumpStatus.ForeColor = Color.Red
                        End If
                End Select
            End If
        Next

        ' Update the timestamp
        LabelLastUpdate.Text = "Última actualización: " & DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
    End Sub


    Private Sub RadioButtonManual_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonManual.CheckedChanged
        ' Enable or disable the manual control panel based on the radio button selection
        PanelManualControl.Enabled = RadioButtonManual.Checked
    End Sub

    Private Sub RadioButtonAutomatic_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonAutomatic.CheckedChanged
        ' When switching to automatic, you might want to send a command to the Arduino
        If RadioButtonAutomatic.Checked AndAlso serialPort.IsOpen Then
            Try
                serialPort.WriteLine("AUTO") ' Send "AUTO" command to Arduino
            Catch ex As Exception
                MessageBox.Show("Error al enviar comando 'Automático': " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    Private Sub ButtonTurnOnPump_Click(sender As Object, e As EventArgs) Handles ButtonTurnOnPump.Click
        ' Send command to turn the pump ON
        If serialPort.IsOpen Then
            Try
                serialPort.WriteLine("ON")
            Catch ex As Exception
                MessageBox.Show("Error al encender la bomba: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        Else
            MessageBox.Show("El puerto serial no está conectado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Sub ButtonTurnOffPump_Click(sender As Object, e As EventArgs) Handles ButtonTurnOffPump.Click
        ' Send command to turn the pump OFF
        If serialPort.IsOpen Then
            Try
                serialPort.WriteLine("OFF")
            Catch ex As Exception
                MessageBox.Show("Error al apagar la bomba: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        Else
            MessageBox.Show("El puerto serial no está conectado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        ' Ensure the serial port is closed when the form is closed
        If serialPort.IsOpen Then
            serialPort.Close()
        End If
    End Sub

#Region "Designer-Generated Code"
    ' NOTE: The following code is auto-generated by the Windows Form Designer.
    ' It is not intended to be modified by hand.

    Private Sub InitializeComponent()
        Me.GroupBoxEnvironmentalData = New System.Windows.Forms.GroupBox()
        Me.PanelHumidityIndicator = New System.Windows.Forms.Panel()
        Me.LabelHumidHeader = New System.Windows.Forms.Label()
        Me.LabelTempHeader = New System.Windows.Forms.Label()
        Me.LabelHumidity = New System.Windows.Forms.Label()
        Me.LabelTemperature = New System.Windows.Forms.Label()
        Me.GroupBoxSystemStatus = New System.Windows.Forms.GroupBox()
        Me.LabelPumpStatus = New System.Windows.Forms.Label()
        Me.LabelPumpStatusHeader = New System.Windows.Forms.Label()
        Me.GroupBoxManualControl = New System.Windows.Forms.GroupBox()
        Me.RadioButtonAutomatic = New System.Windows.Forms.RadioButton()
        Me.RadioButtonManual = New System.Windows.Forms.RadioButton()
        Me.PanelManualControl = New System.Windows.Forms.Panel()
        Me.ButtonTurnOffPump = New System.Windows.Forms.Button()
        Me.ButtonTurnOnPump = New System.Windows.Forms.Button()
        Me.GroupBoxSerialConnection = New System.Windows.Forms.GroupBox()
        Me.ComboBoxPorts = New System.Windows.Forms.ComboBox()
        Me.ButtonConnect = New System.Windows.Forms.Button()
        Me.ButtonDisconnect = New System.Windows.Forms.Button()
        Me.LabelConnectionStatus = New System.Windows.Forms.Label()
        Me.RichTextBoxSerialData = New System.Windows.Forms.RichTextBox()
        Me.LabelLastUpdate = New System.Windows.Forms.Label()
        Me.LabelDeveloper = New System.Windows.Forms.Label()
        Me.GroupBoxEnvironmentalData.SuspendLayout()
        Me.GroupBoxSystemStatus.SuspendLayout()
        Me.GroupBoxManualControl.SuspendLayout()
        Me.PanelManualControl.SuspendLayout()
        Me.GroupBoxSerialConnection.SuspendLayout()
        Me.SuspendLayout()
        '
        'GroupBoxEnvironmentalData
        '
        Me.GroupBoxEnvironmentalData.Controls.Add(Me.PanelHumidityIndicator)
        Me.GroupBoxEnvironmentalData.Controls.Add(Me.LabelHumidHeader)
        Me.GroupBoxEnvironmentalData.Controls.Add(Me.LabelTempHeader)
        Me.GroupBoxEnvironmentalData.Controls.Add(Me.LabelHumidity)
        Me.GroupBoxEnvironmentalData.Controls.Add(Me.LabelTemperature)
        Me.GroupBoxEnvironmentalData.Font = New System.Drawing.Font("Segoe UI", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.GroupBoxEnvironmentalData.Location = New System.Drawing.Point(16, 15)
        Me.GroupBoxEnvironmentalData.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxEnvironmentalData.Name = "GroupBoxEnvironmentalData"
        Me.GroupBoxEnvironmentalData.Padding = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxEnvironmentalData.Size = New System.Drawing.Size(467, 148)
        Me.GroupBoxEnvironmentalData.TabIndex = 0
        Me.GroupBoxEnvironmentalData.TabStop = False
        Me.GroupBoxEnvironmentalData.Text = "Datos Ambientales"
        '
        'PanelHumidityIndicator
        '
        Me.PanelHumidityIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.PanelHumidityIndicator.Location = New System.Drawing.Point(333, 80)
        Me.PanelHumidityIndicator.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.PanelHumidityIndicator.Name = "PanelHumidityIndicator"
        Me.PanelHumidityIndicator.Size = New System.Drawing.Size(53, 49)
        Me.PanelHumidityIndicator.TabIndex = 4
        '
        'LabelHumidHeader
        '
        Me.LabelHumidHeader.AutoSize = True
        Me.LabelHumidHeader.Location = New System.Drawing.Point(27, 92)
        Me.LabelHumidHeader.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelHumidHeader.Name = "LabelHumidHeader"
        Me.LabelHumidHeader.Size = New System.Drawing.Size(164, 23)
        Me.LabelHumidHeader.TabIndex = 3
        Me.LabelHumidHeader.Text = "Humedad del Suelo:"
        '
        'LabelTempHeader
        '
        Me.LabelTempHeader.AutoSize = True
        Me.LabelTempHeader.Location = New System.Drawing.Point(27, 43)
        Me.LabelTempHeader.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelTempHeader.Name = "LabelTempHeader"
        Me.LabelTempHeader.Size = New System.Drawing.Size(110, 23)
        Me.LabelTempHeader.TabIndex = 2
        Me.LabelTempHeader.Text = "Temperatura:"
        '
        'LabelHumidity
        '
        Me.LabelHumidity.AutoSize = True
        Me.LabelHumidity.Font = New System.Drawing.Font("Segoe UI Semibold", 14.25!, System.Drawing.FontStyle.Bold)
        Me.LabelHumidity.ForeColor = System.Drawing.Color.Blue
        Me.LabelHumidity.Location = New System.Drawing.Point(200, 86)
        Me.LabelHumidity.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelHumidity.Name = "LabelHumidity"
        Me.LabelHumidity.Size = New System.Drawing.Size(50, 32)
        Me.LabelHumidity.TabIndex = 1
        Me.LabelHumidity.Text = "315"
        '
        'LabelTemperature
        '
        Me.LabelTemperature.AutoSize = True
        Me.LabelTemperature.Font = New System.Drawing.Font("Segoe UI Semibold", 14.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.LabelTemperature.ForeColor = System.Drawing.Color.Red
        Me.LabelTemperature.Location = New System.Drawing.Point(200, 37)
        Me.LabelTemperature.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelTemperature.Name = "LabelTemperature"
        Me.LabelTemperature.Size = New System.Drawing.Size(87, 32)
        Me.LabelTemperature.TabIndex = 0
        Me.LabelTemperature.Text = "29,1 °C"
        '
        'GroupBoxSystemStatus
        '
        Me.GroupBoxSystemStatus.Controls.Add(Me.LabelPumpStatus)
        Me.GroupBoxSystemStatus.Controls.Add(Me.LabelPumpStatusHeader)
        Me.GroupBoxSystemStatus.Font = New System.Drawing.Font("Segoe UI", 9.75!)
        Me.GroupBoxSystemStatus.Location = New System.Drawing.Point(507, 15)
        Me.GroupBoxSystemStatus.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxSystemStatus.Name = "GroupBoxSystemStatus"
        Me.GroupBoxSystemStatus.Padding = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxSystemStatus.Size = New System.Drawing.Size(523, 148)
        Me.GroupBoxSystemStatus.TabIndex = 1
        Me.GroupBoxSystemStatus.TabStop = False
        Me.GroupBoxSystemStatus.Text = "Estado del Sistema"
        '
        'LabelPumpStatus
        '
        Me.LabelPumpStatus.AutoSize = True
        Me.LabelPumpStatus.Font = New System.Drawing.Font("Segoe UI", 15.75!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.LabelPumpStatus.ForeColor = System.Drawing.Color.Green
        Me.LabelPumpStatus.Location = New System.Drawing.Point(173, 62)
        Me.LabelPumpStatus.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelPumpStatus.Name = "LabelPumpStatus"
        Me.LabelPumpStatus.Size = New System.Drawing.Size(172, 37)
        Me.LabelPumpStatus.TabIndex = 1
        Me.LabelPumpStatus.Text = "ENCENDIDA"
        '
        'LabelPumpStatusHeader
        '
        Me.LabelPumpStatusHeader.AutoSize = True
        Me.LabelPumpStatusHeader.Location = New System.Drawing.Point(27, 71)
        Me.LabelPumpStatusHeader.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelPumpStatusHeader.Name = "LabelPumpStatusHeader"
        Me.LabelPumpStatusHeader.Size = New System.Drawing.Size(141, 23)
        Me.LabelPumpStatusHeader.TabIndex = 0
        Me.LabelPumpStatusHeader.Text = "Estado del Riego:"
        '
        'GroupBoxManualControl
        '
        Me.GroupBoxManualControl.Controls.Add(Me.RadioButtonAutomatic)
        Me.GroupBoxManualControl.Controls.Add(Me.RadioButtonManual)
        Me.GroupBoxManualControl.Controls.Add(Me.PanelManualControl)
        Me.GroupBoxManualControl.Font = New System.Drawing.Font("Segoe UI", 9.75!)
        Me.GroupBoxManualControl.Location = New System.Drawing.Point(16, 185)
        Me.GroupBoxManualControl.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxManualControl.Name = "GroupBoxManualControl"
        Me.GroupBoxManualControl.Padding = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxManualControl.Size = New System.Drawing.Size(467, 185)
        Me.GroupBoxManualControl.TabIndex = 2
        Me.GroupBoxManualControl.TabStop = False
        Me.GroupBoxManualControl.Text = "Control Manual"
        '
        'RadioButtonAutomatic
        '
        Me.RadioButtonAutomatic.AutoSize = True
        Me.RadioButtonAutomatic.Checked = True
        Me.RadioButtonAutomatic.Location = New System.Drawing.Point(67, 37)
        Me.RadioButtonAutomatic.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.RadioButtonAutomatic.Name = "RadioButtonAutomatic"
        Me.RadioButtonAutomatic.Size = New System.Drawing.Size(120, 27)
        Me.RadioButtonAutomatic.TabIndex = 0
        Me.RadioButtonAutomatic.TabStop = True
        Me.RadioButtonAutomatic.Text = "Automático"
        Me.RadioButtonAutomatic.UseVisualStyleBackColor = True
        '
        'RadioButtonManual
        '
        Me.RadioButtonManual.AutoSize = True
        Me.RadioButtonManual.Location = New System.Drawing.Point(247, 37)
        Me.RadioButtonManual.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.RadioButtonManual.Name = "RadioButtonManual"
        Me.RadioButtonManual.Size = New System.Drawing.Size(88, 27)
        Me.RadioButtonManual.TabIndex = 1
        Me.RadioButtonManual.Text = "Manual"
        Me.RadioButtonManual.UseVisualStyleBackColor = True
        '
        'PanelManualControl
        '
        Me.PanelManualControl.Controls.Add(Me.ButtonTurnOffPump)
        Me.PanelManualControl.Controls.Add(Me.ButtonTurnOnPump)
        Me.PanelManualControl.Location = New System.Drawing.Point(20, 74)
        Me.PanelManualControl.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.PanelManualControl.Name = "PanelManualControl"
        Me.PanelManualControl.Size = New System.Drawing.Size(427, 86)
        Me.PanelManualControl.TabIndex = 2
        '
        'ButtonTurnOffPump
        '
        Me.ButtonTurnOffPump.Location = New System.Drawing.Point(227, 18)
        Me.ButtonTurnOffPump.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.ButtonTurnOffPump.Name = "ButtonTurnOffPump"
        Me.ButtonTurnOffPump.Size = New System.Drawing.Size(187, 49)
        Me.ButtonTurnOffPump.TabIndex = 1
        Me.ButtonTurnOffPump.Text = "Apagar Bomba"
        Me.ButtonTurnOffPump.UseVisualStyleBackColor = True
        '
        'ButtonTurnOnPump
        '
        Me.ButtonTurnOnPump.Location = New System.Drawing.Point(13, 18)
        Me.ButtonTurnOnPump.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.ButtonTurnOnPump.Name = "ButtonTurnOnPump"
        Me.ButtonTurnOnPump.Size = New System.Drawing.Size(187, 49)
        Me.ButtonTurnOnPump.TabIndex = 0
        Me.ButtonTurnOnPump.Text = "Encender Bomba"
        Me.ButtonTurnOnPump.UseVisualStyleBackColor = True
        '
        'GroupBoxSerialConnection
        '
        Me.GroupBoxSerialConnection.Controls.Add(Me.ComboBoxPorts)
        Me.GroupBoxSerialConnection.Controls.Add(Me.ButtonConnect)
        Me.GroupBoxSerialConnection.Controls.Add(Me.ButtonDisconnect)
        Me.GroupBoxSerialConnection.Controls.Add(Me.LabelConnectionStatus)
        Me.GroupBoxSerialConnection.Controls.Add(Me.RichTextBoxSerialData)
        Me.GroupBoxSerialConnection.Font = New System.Drawing.Font("Segoe UI", 9.75!)
        Me.GroupBoxSerialConnection.Location = New System.Drawing.Point(507, 185)
        Me.GroupBoxSerialConnection.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxSerialConnection.Name = "GroupBoxSerialConnection"
        Me.GroupBoxSerialConnection.Padding = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.GroupBoxSerialConnection.Size = New System.Drawing.Size(523, 308)
        Me.GroupBoxSerialConnection.TabIndex = 3
        Me.GroupBoxSerialConnection.TabStop = False
        Me.GroupBoxSerialConnection.Text = "Conexión Serial"
        '
        'ComboBoxPorts
        '
        Me.ComboBoxPorts.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.ComboBoxPorts.FormattingEnabled = True
        Me.ComboBoxPorts.Location = New System.Drawing.Point(20, 34)
        Me.ComboBoxPorts.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.ComboBoxPorts.Name = "ComboBoxPorts"
        Me.ComboBoxPorts.Size = New System.Drawing.Size(159, 29)
        Me.ComboBoxPorts.TabIndex = 0
        '
        'ButtonConnect
        '
        Me.ButtonConnect.Location = New System.Drawing.Point(193, 31)
        Me.ButtonConnect.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.ButtonConnect.Name = "ButtonConnect"
        Me.ButtonConnect.Size = New System.Drawing.Size(147, 37)
        Me.ButtonConnect.TabIndex = 1
        Me.ButtonConnect.Text = "Conectar"
        Me.ButtonConnect.UseVisualStyleBackColor = True
        '
        'ButtonDisconnect
        '
        Me.ButtonDisconnect.Location = New System.Drawing.Point(353, 31)
        Me.ButtonDisconnect.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.ButtonDisconnect.Name = "ButtonDisconnect"
        Me.ButtonDisconnect.Size = New System.Drawing.Size(147, 37)
        Me.ButtonDisconnect.TabIndex = 2
        Me.ButtonDisconnect.Text = "Desconectar"
        Me.ButtonDisconnect.UseVisualStyleBackColor = True
        '
        'LabelConnectionStatus
        '
        Me.LabelConnectionStatus.AutoSize = True
        Me.LabelConnectionStatus.Font = New System.Drawing.Font("Segoe UI Semibold", 9.75!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.LabelConnectionStatus.ForeColor = System.Drawing.Color.Red
        Me.LabelConnectionStatus.Location = New System.Drawing.Point(27, 86)
        Me.LabelConnectionStatus.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelConnectionStatus.Name = "LabelConnectionStatus"
        Me.LabelConnectionStatus.Size = New System.Drawing.Size(178, 23)
        Me.LabelConnectionStatus.TabIndex = 3
        Me.LabelConnectionStatus.Text = "Estado: No conectado"
        '
        'RichTextBoxSerialData
        '
        Me.RichTextBoxSerialData.Location = New System.Drawing.Point(20, 123)
        Me.RichTextBoxSerialData.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.RichTextBoxSerialData.Name = "RichTextBoxSerialData"
        Me.RichTextBoxSerialData.ReadOnly = True
        Me.RichTextBoxSerialData.Size = New System.Drawing.Size(479, 165)
        Me.RichTextBoxSerialData.TabIndex = 4
        Me.RichTextBoxSerialData.Text = ""
        '
        'LabelLastUpdate
        '
        Me.LabelLastUpdate.AutoSize = True
        Me.LabelLastUpdate.ForeColor = System.Drawing.SystemColors.ControlDarkDark
        Me.LabelLastUpdate.Location = New System.Drawing.Point(16, 511)
        Me.LabelLastUpdate.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelLastUpdate.Name = "LabelLastUpdate"
        Me.LabelLastUpdate.Size = New System.Drawing.Size(247, 16)
        Me.LabelLastUpdate.TabIndex = 4
        Me.LabelLastUpdate.Text = "Última actualización: 02/07/2025 07:43:10"
        '
        'LabelDeveloper
        '
        Me.LabelDeveloper.AutoSize = True
        Me.LabelDeveloper.ForeColor = System.Drawing.SystemColors.ControlDarkDark
        Me.LabelDeveloper.Location = New System.Drawing.Point(846, 497)
        Me.LabelDeveloper.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelDeveloper.Name = "LabelDeveloper"
        Me.LabelDeveloper.Size = New System.Drawing.Size(184, 48)
        Me.LabelDeveloper.TabIndex = 5
        Me.LabelDeveloper.Text = "Desarrollado por Luis Marken" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "Braulio Angulo" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "Irma Diaz"
        '
        'MainForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1045, 543)
        Me.Controls.Add(Me.LabelDeveloper)
        Me.Controls.Add(Me.LabelLastUpdate)
        Me.Controls.Add(Me.GroupBoxSerialConnection)
        Me.Controls.Add(Me.GroupBoxManualControl)
        Me.Controls.Add(Me.GroupBoxSystemStatus)
        Me.Controls.Add(Me.GroupBoxEnvironmentalData)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.MaximizeBox = False
        Me.Name = "MainForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Estación de Monitoreo y Riego Automatizado"
        Me.GroupBoxEnvironmentalData.ResumeLayout(False)
        Me.GroupBoxEnvironmentalData.PerformLayout()
        Me.GroupBoxSystemStatus.ResumeLayout(False)
        Me.GroupBoxSystemStatus.PerformLayout()
        Me.GroupBoxManualControl.ResumeLayout(False)
        Me.GroupBoxManualControl.PerformLayout()
        Me.PanelManualControl.ResumeLayout(False)
        Me.GroupBoxSerialConnection.ResumeLayout(False)
        Me.GroupBoxSerialConnection.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
#End Region

End Class

Module Program
    ''' <summary>
    ''' The main entry point for the application.
    ''' </summary>
    <STAThread()>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub
End Module

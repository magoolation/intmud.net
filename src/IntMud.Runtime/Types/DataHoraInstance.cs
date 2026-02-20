namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a datahora (date/time) instance.
/// Uses raw fields matching C++ TVarDataHora for exact compatibility.
/// DataNum() counts days since 1/1/1 (year 1, month 1, day 1 = day 0).
/// </summary>
public sealed class DataHoraInstance
{
    // Raw fields matching C++ TVarDataHora
    private int _ano = 1;   // Year [1, 9999]
    private int _mes = 1;   // Month [1, 12]
    private int _dia = 1;   // Day [1, 31]
    private int _hora = 0;  // Hour [0, 23]
    private int _min = 0;   // Minute [0, 59]
    private int _seg = 0;   // Second [0, 59]

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Set to current local date/time.
    /// </summary>
    public void Agora()
    {
        var now = DateTime.Now;
        _ano = now.Year;
        _mes = now.Month;
        _dia = now.Day;
        _hora = now.Hour;
        _min = now.Minute;
        _seg = now.Second;
    }

    public int Ano
    {
        get => _ano;
        set
        {
            _ano = Math.Clamp(value, 1, 9999);
            // If Feb 29 and no longer leap year, adjust
            if (_mes == 2 && _dia == 29 && !IsLeapYear(_ano))
                _dia = 28;
        }
    }

    public int Mes
    {
        get => _mes;
        set
        {
            _mes = Math.Clamp(value, 1, 12);
            int maxDay = DiasMes();
            if (_dia > maxDay)
                _dia = maxDay;
        }
    }

    public int Dia
    {
        get => _dia;
        set
        {
            int maxDay = DiasMes();
            _dia = Math.Clamp(value, 1, maxDay);
        }
    }

    public int Hora
    {
        get => _hora;
        set => _hora = Math.Clamp(value, 0, 23);
    }

    public int Min
    {
        get => _min;
        set => _min = Math.Clamp(value, 0, 59);
    }

    public int Seg
    {
        get => _seg;
        set => _seg = Math.Clamp(value, 0, 59);
    }

    /// <summary>
    /// Set a new date with clamping.
    /// </summary>
    public void NovaData(int ano, int mes, int dia)
    {
        _ano = Math.Clamp(ano, 1, 9999);
        _mes = Math.Clamp(mes, 1, 12);
        _dia = Math.Clamp(dia, 1, 31);
        int maxDay = DiasMes();
        if (_dia > maxDay)
            _dia = maxDay;
    }

    /// <summary>
    /// Set a new time with clamping.
    /// </summary>
    public void NovaHora(int hora, int min, int seg)
    {
        _hora = Math.Clamp(hora, 0, 23);
        _min = Math.Clamp(min, 0, 59);
        _seg = Math.Clamp(seg, 0, 59);
    }

    /// <summary>
    /// Day of week: (DataNum() + 1) % 7.
    /// 0=Sunday, 1=Monday, ..., 6=Saturday (same as C++).
    /// </summary>
    public int DiaSem => (DataNum() + 1) % 7;

    /// <summary>
    /// Check if current year is a leap year.
    /// </summary>
    public bool Bissexto => IsLeapYear(_ano);

    /// <summary>
    /// Number of days since 1/1/1 (year 1). Matches C++ DataNum() exactly.
    /// </summary>
    public int NumDias => DataNum();

    /// <summary>
    /// Set date from day count since 1/1/1.
    /// </summary>
    public void SetNumDias(int dias)
    {
        if (dias < 0) dias = 0;
        if (dias > 3652058) dias = 3652058; // 31/12/9999
        NumData(dias);
    }

    /// <summary>
    /// Number of seconds since midnight.
    /// </summary>
    public int NumSeg => (_hora * 60 + _min) * 60 + _seg;

    /// <summary>
    /// Set time from seconds since midnight.
    /// </summary>
    public void SetNumSeg(int valor)
    {
        if (valor < 0) valor = 0;
        if (valor > 24 * 60 * 60 - 1) valor = 24 * 60 * 60 - 1;
        _seg = valor % 60;
        valor /= 60;
        _min = valor % 60;
        _hora = valor / 60;
    }

    /// <summary>
    /// Total as double: DataNum() * 86400.0 + seconds.
    /// C++ returns double for numtotal; getInt truncates via DoubleToInt.
    /// </summary>
    public double NumTotal => (_hora * 60 + _min) * 60 + _seg + DataNum() * 86400.0;

    /// <summary>
    /// Set date+time from total double value.
    /// </summary>
    public void SetNumTotal(double valor)
    {
        if (!(valor > 0)) // handles NaN
        {
            NumData(0);
            _seg = 0; _min = 0; _hora = 0;
            return;
        }
        double data = Math.Truncate(valor / 86400.0);
        NumData((int)Math.Clamp(data, 0, 3652058));
        int hns = (int)Math.Round(valor - data * 86400.0);
        if (hns < 0) hns = 0;
        if (hns > 24 * 60 * 60 - 1) hns = 24 * 60 * 60 - 1;
        _seg = hns % 60;
        hns /= 60;
        _min = hns % 60;
        _hora = hns / 60;
    }

    /// <summary>
    /// Move to previous day. Matches C++ FuncAntes.
    /// </summary>
    public void Antes()
    {
        if (_dia > 1)
            _dia--;
        else if (_mes > 1)
        {
            _mes--;
            _dia = DiasMes();
        }
        else if (_ano > 1)
        {
            _dia = 31;
            _mes = 12;
            _ano--;
        }
    }

    /// <summary>
    /// Move to next day. Matches C++ FuncDepois.
    /// </summary>
    public void Depois()
    {
        if (_dia < DiasMes())
            _dia++;
        else if (_mes < 12)
        {
            _dia = 1;
            _mes++;
        }
        else if (_ano < 9999)
        {
            _dia = 1;
            _mes = 1;
            _ano++;
        }
    }

    /// <summary>
    /// Convert date to days since 1/1/1. Matches C++ DataNum() exactly.
    /// </summary>
    public int DataNum()
    {
        int[] diasMesTabela = {
            0, 31, 31+28, 31+28+31,
            31+28+31+30,
            31+28+31+30+31,
            31+28+31+30+31+30,
            31+28+31+30+31+30+31,
            31+28+31+30+31+30+31+31,
            31+28+31+30+31+30+31+31+30,
            31+28+31+30+31+30+31+31+30+31,
            31+28+31+30+31+30+31+31+30+31+30
        };

        int valor = _dia - 1;
        valor += diasMesTabela[_mes - 1];
        if (_mes > 2)
            valor += IsLeapYear(_ano) ? 1 : 0;

        int a = _ano - 1;
        valor += a * 1461 / 4 - a / 100 + a / 400;
        return valor;
    }

    /// <summary>
    /// Convert days since 1/1/1 back to date. Matches C++ NumData() exactly.
    /// </summary>
    public void NumData(int dias)
    {
        // 400 years = 146097 days
        int a = (dias * 4 + 3) / 146097;
        dias -= a * 146097 / 4;

        // Within 100 years: every 4 years, 1 is leap
        int b = (dias * 4 + 3) / 1461;
        dias -= b * 1461 / 4;

        _ano = a * 100 + b + 1;

        // Check if leap year
        int bissexto = (b % 4 != 3 ? 0 : b != 99 ? 1 : a % 4 == 3 ? 1 : 0);

        // Simulate February with 30 days
        if (dias >= 31 + 28 + bissexto)
            dias += 2 - bissexto;

        // Cycle repeats every 7 months, totaling 214 days
        int x = (dias * 7 + 3) / 214;
        _mes = x + 1;
        _dia = 1 + dias - (x * 214 + 3) / 7;
    }

    /// <summary>
    /// Get number of days in current month. Matches C++ DiasMes().
    /// </summary>
    public int DiasMes()
    {
        if (_mes == 2)
            return IsLeapYear(_ano) ? 29 : 28;
        if (_mes < 8)
            return 30 + _mes % 2;
        return 31 - _mes % 2;
    }

    /// <summary>
    /// Parse 14-digit date string YYYYMMDDHHMMSS. For ArqSav integration.
    /// </summary>
    public void LerSav(string texto)
    {
        if (texto.Length < 14) return;
        for (int i = 0; i < 14; i++)
            if (texto[i] < '0' || texto[i] > '9') return;

        int x = (texto[0] - '0') * 1000 + (texto[1] - '0') * 100 +
                (texto[2] - '0') * 10 + texto[3] - '0';
        _ano = Math.Clamp(x, 1, 9999);
        x = (texto[4] - '0') * 10 + texto[5] - '0';
        _mes = Math.Clamp(x, 1, 12);
        x = (texto[6] - '0') * 10 + texto[7] - '0';
        int maxDay = DiasMes();
        _dia = Math.Clamp(x, 1, maxDay);
        x = (texto[8] - '0') * 10 + texto[9] - '0';
        _hora = Math.Min(x, 23);
        x = (texto[10] - '0') * 10 + texto[11] - '0';
        _min = Math.Min(x, 59);
        x = (texto[12] - '0') * 10 + texto[13] - '0';
        _seg = Math.Min(x, 59);
    }

    /// <summary>
    /// Format as 14-digit string YYYYMMDDHHMMSS. For ArqSav integration.
    /// </summary>
    public string SalvarSav()
    {
        return $"{_ano:D4}{_mes:D2}{_dia:D2}{_hora:D2}{_min:D2}{_seg:D2}";
    }

    private static bool IsLeapYear(int year)
    {
        return (year % 4 == 0) && ((year % 100 != 0) || (year % 400 == 0));
    }

    public override string ToString() => $"{_ano:D4}-{_mes:D2}-{_dia:D2} {_hora:D2}:{_min:D2}:{_seg:D2}";
}

using IntMud.Compiler.Parsing;
using Xunit;

namespace IntMud.Compiler.Tests;

public class IntMudConfigParserTests
{
    [Fact]
    public void Parse_EmptyFile_ReturnsEmptyConfig()
    {
        var parser = new IntMudConfigParser();
        var config = parser.Parse("", "test.int");

        Assert.Empty(config.Includes);
        Assert.Equal(10000, config.ExecLimit); // Default value
        Assert.False(config.TelaTxt);
    }

    [Fact]
    public void Parse_WithComments_IgnoresComments()
    {
        var content = @"
# This is a comment
incluir = adm/
# Another comment
";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Single(config.Includes);
        Assert.Equal("adm/", config.Includes[0]);
    }

    [Fact]
    public void Parse_MultipleIncludes_AddsAll()
    {
        var content = @"
incluir = adm/
incluir = cmd/
incluir = misc/
incluir = obj/
incluir = config/
incluir = areas/
";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Equal(6, config.Includes.Count);
        Assert.Contains("adm/", config.Includes);
        Assert.Contains("cmd/", config.Includes);
        Assert.Contains("misc/", config.Includes);
        Assert.Contains("obj/", config.Includes);
        Assert.Contains("config/", config.Includes);
        Assert.Contains("areas/", config.Includes);
    }

    [Fact]
    public void Parse_ExecLimit_SetsValue()
    {
        var content = "exec = 5000";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Equal(5000, config.ExecLimit);
    }

    [Fact]
    public void Parse_TelaTxtEnabled_SetsTrue()
    {
        var content = "telatxt = 1";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.True(config.TelaTxt);
    }

    [Fact]
    public void Parse_TelaTxtDisabled_SetsFalse()
    {
        var content = "telatxt = 0";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.False(config.TelaTxt);
    }

    [Fact]
    public void Parse_LogMode_SetsValue()
    {
        var content = "log = 2";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Equal(2, config.LogMode);
    }

    [Fact]
    public void Parse_ErrorMode_SetsValue()
    {
        var content = "err = 2";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Equal(2, config.ErrorMode);
    }

    [Fact]
    public void Parse_FullAccess_SetsTrue()
    {
        var content = "completo = 1";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.True(config.FullAccess);
    }

    [Fact]
    public void Parse_UnknownDirective_StoredInOtherSettings()
    {
        var content = "custom_setting = some_value";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.True(config.OtherSettings.ContainsKey("custom_setting"));
        Assert.Equal("some_value", config.OtherSettings["custom_setting"]);
    }

    [Fact]
    public void Parse_InlineComment_StrippedFromValue()
    {
        var content = "exec = 10000 # Maximum instructions";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Equal(10000, config.ExecLimit);
    }

    [Fact]
    public void Parse_RealMudIntFile_ParsesCorrectly()
    {
        var content = @"# Nomes dos outros arquivos que compõem o programa começam com:
incluir = adm/
incluir = cmd/
incluir = misc/
incluir = obj/
incluir = config/
incluir = areas/

# Quantas instruções uma função chamada pelo programa pode
# executar antes do controle retornar ao programa
exec = 10000

# Se deve abrir uma janela de texto - variável telatxt
telatxt = 1

# Aonde apresentar mensagens de erro no programa
log = 0

# Erros em blocos de instruções:
# 0=ignorar, 1=permitir apenas FimSe sem Se, 2=checar tudo
err = 1

# Se o programa roda sem restrições (0=não, 1=sim)
completo = 0
";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "mud.int");

        Assert.Equal(6, config.Includes.Count);
        Assert.Equal(10000, config.ExecLimit);
        Assert.True(config.TelaTxt);
        Assert.Equal(0, config.LogMode);
        Assert.Equal(1, config.ErrorMode);
        Assert.False(config.FullAccess);
    }

    [Fact]
    public void Parse_WhitespaceAroundEquals_HandledCorrectly()
    {
        var content = "  exec   =   5000  ";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Equal(5000, config.ExecLimit);
    }

    [Fact]
    public void Parse_CaseInsensitiveKeys_Works()
    {
        var content = @"
INCLUIR = adm/
EXEC = 5000
TELATXT = 1
";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Single(config.Includes);
        Assert.Equal(5000, config.ExecLimit);
        Assert.True(config.TelaTxt);
    }

    [Fact]
    public void Parse_LinesWithoutEquals_Ignored()
    {
        var content = @"
some random text without equals
incluir = adm/
more text
";
        var parser = new IntMudConfigParser();
        var config = parser.Parse(content, "test.int");

        Assert.Single(config.Includes);
    }
}

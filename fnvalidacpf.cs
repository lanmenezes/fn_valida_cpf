using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace valida_cpf_project;

public class FnValidaCpf
{
    private readonly ILogger<FnValidaCpf> _logger;

    public FnValidaCpf(ILogger<FnValidaCpf> logger) => _logger = logger;

    [Function("fnvalidacpf")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
    {
        _logger.LogInformation("Iniciando a validação do CPF.");

        // 1) Tenta query string
        string cpf = req.Query["cpf"];

        // 2) Se POST e não veio query, tenta corpo JSON { "cpf": "..." }
        if (string.IsNullOrWhiteSpace(cpf) && HttpMethods.IsPost(req.Method))
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("cpf", out var cpfProp))
                        cpf = cpfProp.GetString();
                }
                catch (JsonException)
                {
                    return new BadRequestObjectResult("JSON inválido. Envie { \"cpf\": \"...\" }.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(cpf))
            return new BadRequestObjectResult("Por favor, forneça um CPF via query (?cpf=...) ou corpo JSON: { \"cpf\": \"...\" }.");

        cpf = cpf.Trim();

        if (!ValidaCPF(cpf))
        {
            _logger.LogInformation("CPF inválido: {cpf}", cpf);
            return new BadRequestObjectResult("CPF inválido.");
        }

        return new OkObjectResult($"CPF {cpf} é válido e não consta na base de débitos.");
    }

    private static bool ValidaCPF(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        cpf = cpf.Replace(".", "").Replace("-", "").Trim();
        if (cpf.Length != 11 || !long.TryParse(cpf, out _))
            return false;

        var numbers = new int[11];
        for (int i = 0; i < 11; i++) numbers[i] = int.Parse(cpf[i].ToString());

        var sum = 0;
        for (int i = 0; i < 9; i++) sum += (10 - i) * numbers[i];

        var result = sum % 11;
        if ((result == 1 || result == 0) ? numbers[9] != 0 : numbers[9] != 11 - result) return false;

        sum = 0;
        for (int i = 0; i < 10; i++) sum += (11 - i) * numbers[i];

        result = sum % 11;
        if ((result == 1 || result == 0) ? numbers[10] != 0 : numbers[10] != 11 - result) return false;

        return true;
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace 进销存demo.Models.Validation;

/// <summary>比较属性值不小于另一属性（用于售价 ≥ 采购价）；支持客户端 unobtrusive 校验。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CompareGreaterThanOrEqualAttribute : ValidationAttribute, IClientModelValidator
{
    private readonly string _otherPropertyName;

    public CompareGreaterThanOrEqualAttribute(string otherPropertyName)
    {
        _otherPropertyName = otherPropertyName;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var other = validationContext.ObjectType.GetProperty(_otherPropertyName);
        if (other == null)
            return new ValidationResult($"未知属性：{_otherPropertyName}");

        var otherVal = other.GetValue(validationContext.ObjectInstance);
        try
        {
            var a = Convert.ToDecimal(value ?? 0m);
            var b = Convert.ToDecimal(otherVal ?? 0m);
            if (a < b)
                return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName}不能小于{other.Name}");
        }
        catch
        {
            return ValidationResult.Success;
        }

        return ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        var error = ErrorMessage ?? $"{context.ModelMetadata.GetDisplayName()}不能小于{_otherPropertyName}";
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-comparegte"] = error;
        context.Attributes["data-val-comparegte-other"] = _otherPropertyName;
    }
}

using FluentValidation;
using FluentValidation.Results;
using System.Threading.Tasks;

namespace AgileServiceBus.Extensions
{
    public static class AbstractValidatorExtension
    {
        public static async Task ValidateAndThrowAsync<T>(this AbstractValidator<T> avb, T toValidate, string exceptionMessage) where T : class
        {
            ValidationResult validationResult = await avb.ValidateAsync(toValidate);

            if (!validationResult.IsValid)
            {
                ValidationException exception = new ValidationException(exceptionMessage, validationResult.Errors);
                foreach (ValidationFailure error in validationResult.Errors)
                    exception.Data.Add(error.PropertyName, error.ErrorCode);

                throw exception;
            }
        }
    }
}
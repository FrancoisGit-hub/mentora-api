using Mentora.API.Controllers.Coach;
using Mentora.API.Controllers.Member;
using Mentora.Core.Enums;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Mentora.API.Swagger;

/// <summary>
/// On the exercise list endpoints: constrains the <c>level</c> query parameter to its wire values
/// and, on the coach list, marks the legacy <c>muscleGroup</c> / <c>equipment</c> filters as deprecated.
/// </summary>
public sealed class ExerciseListOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var declaring = context.MethodInfo.DeclaringType;
        var isCoachList  = declaring == typeof(CoachExercisesController)
                           && context.MethodInfo.Name == nameof(CoachExercisesController.GetAll);
        var isMemberList = declaring == typeof(MemberExercisesController)
                           && context.MethodInfo.Name == nameof(MemberExercisesController.GetAll);
        if (!isCoachList && !isMemberList)
            return;

        var level = operation.Parameters?.FirstOrDefault(p => p.Name == "level");
        if (level is not null)
            level.Schema.Enum = EnumMappings.ExerciseLevelMapping.WireValues
                .Select(v => (IOpenApiAny)new OpenApiString(v))
                .ToList();

        if (!isCoachList)
            return;

        foreach (var name in new[] { "muscleGroup", "equipment" })
        {
            var param = operation.Parameters?.FirstOrDefault(p => p.Name == name);
            if (param is not null)
                param.Deprecated = true;
        }
    }
}

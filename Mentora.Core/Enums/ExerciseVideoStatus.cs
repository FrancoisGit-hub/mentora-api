namespace Mentora.Core.Enums;

/// <summary>
/// Processing state of an exercise video asset.
/// Persisted as VARCHAR(10) UPPERCASE: WAITING / READY / ERRORED.
/// </summary>
public enum ExerciseVideoStatus
{
    Waiting,
    Ready,
    Errored
}

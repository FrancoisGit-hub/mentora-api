namespace Mentora.Core.Enums;

/// <summary>
/// Processing state of an exercise video.
/// Persisted as VARCHAR(10) UPPERCASE: UPLOADING / WAITING / READY / ERRORED.
/// UPLOADING (Lot 12.3a): a MUX direct upload exists, the file has not reached MUX yet (no asset id).
/// WAITING: MUX holds the asset and is preparing it.
/// </summary>
public enum ExerciseVideoStatus
{
    Uploading,
    Waiting,
    Ready,
    Errored
}

public static class GameConstants
{
    public const int NUM_LANES = 12;
    public const float LANE_SPACING = 0.85f;
    public const float NOTE_Z_SPAWN = -95f;
    public const float NOTE_Z_HIT = 4.2f;
    public const float NOTE_Z_DEAD = 9.0f;
    public const float NOTE_SPEED = 36f;

    // (4.2 - (-95)) / 36 ≈ 2.756s
    public static readonly float TRAVEL_TIME = (NOTE_Z_HIT - NOTE_Z_SPAWN) / NOTE_SPEED;

    // HTML版に合わせた値: PERFECT ±2.2, GOOD ±4.2 units
    public static readonly float HIT_WINDOW_PERFECT = 2.2f / NOTE_SPEED;
    public static readonly float HIT_WINDOW_GOOD = 4.2f / NOTE_SPEED;

    // key group → lane pair mapping
    public static readonly int[,] KEY_LANES = {
        { 0,  1 },  // A
        { 2,  3 },  // S
        { 4,  5 },  // D
        { 6,  7 },  // J
        { 8,  9 },  // K
        { 10, 11 }, // L
    };
}

namespace ProjectNeo.Characters
{
    public static class PlayerLocomotionRules
    {
        public static float SpeedMultiplier(bool crouching, bool sprinting, PlayerControlSettings settings)
        {
            if (crouching) return settings.CrouchSpeedMultiplier;
            return sprinting ? settings.SprintSpeedMultiplier : 1f;
        }
    }
}

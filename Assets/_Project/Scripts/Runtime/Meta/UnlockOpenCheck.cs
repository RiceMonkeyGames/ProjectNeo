namespace ProjectNeo.Meta
{
    /// <summary>해금 항목을 열 수 있는지 확인한 결과.</summary>
    public enum UnlockOpenCheck
    {
        Ok = 0,
        UnknownEntry = 1,
        AlreadyOpened = 2,
        MissingPrerequisite = 3,
        NotEnoughPoints = 4,
    }
}

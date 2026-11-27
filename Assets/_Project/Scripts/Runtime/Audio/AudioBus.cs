namespace ProjectNeo.Audio
{
    /// <summary>소리가 지나가는 믹서 통로. 저장 파일과 에셋에 숫자로 남으므로 값을 바꾸지 말고 끝에만 덧붙인다.</summary>
    public enum AudioBus
    {
        Master = 0,
        Bgm = 1,
        Sfx = 2,
        Ui = 3,
        /// <summary>대사용 예약(Q-83). 지금은 쓰는 소리가 없다.</summary>
        Voice = 4,
    }
}

namespace QuizApi.Models.EnumModel
{
    public enum Part
    {
        Tamil=1,
        Gk,
        Maths,
        NA
    }
    public enum TamilUnit
    {
        NA,
        ilakkanam,
        Sollagaraathi,
        EluthumThiran,
        Kalaisorkal,
        Vaasithal,
        Translation,
        TamilArinjarAndThoondu
    }
    public enum SubscriptionType
    {
        Free = 1,
        Pro,
        ProMax
    }
    public enum GKUnit{
        NA,
        Science,
        Geography,
        IndianHistoryAndINM,
        IndianPolity,
        EcomimocsAndTNAdminstration,
        Unit6
    }
    public enum MathsUnit{
        NA,
        Apptitue,
        Reasoning
    }
    public enum Standard{
        NA = 0,
        SixthStd=6,
        SeventhStd,
        EighthStd,
        NinethStd,
        TenthStd,
        ElevelthStd,
        TwelthStd
    }
}
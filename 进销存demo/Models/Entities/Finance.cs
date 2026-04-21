namespace 进销存demo.Models.Entities
{
    public enum ReceivableStatus
    {
        Outstanding = 0,
        Paid = 1,
        WrittenOff = 2
    }

    public enum PayableStatus
    {
        Outstanding = 0,
        Paid = 1,
        WrittenOff = 2
    }

    public enum PaymentMethod
    {
        Cash = 0,
        Bank = 1,
        Alipay = 2,
        WeChat = 3,
        Other = 4
    }
}

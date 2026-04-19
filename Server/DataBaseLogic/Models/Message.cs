namespace Server.Database.Models;

class Messsage {
    public int message_id { get; set; }
    public TimeSpan send_time { get; set; }
    public string body { get; set; }
    public int sender_id { get; set; }
}
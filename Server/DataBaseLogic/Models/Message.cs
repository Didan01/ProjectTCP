namespace Server.Database.Models;

class Messsage {
    public int message_id { get; set; }
    public TimeOnly send_time { get; set; }
    public string body { get; set; }
    public int sender_id { get; set; }
}
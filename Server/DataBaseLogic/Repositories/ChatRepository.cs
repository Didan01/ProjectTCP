using Dapper;
using Server.Database.Models;

namespace Server.Database.Repositories;

class ChatRepository: IDisposable {
    private readonly DapperContext dapperContext;
    public ChatRepository() {
        dapperContext = new DapperContext();
    }
    public bool addUser(int user_id, int chat_id) {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            try {
                string query = "INSERT INTO chat_members(user_id, chat_id) VALUES (@user_id, @chat_id)";
                conn.Execute(query, new {user_id = user_id, chat_id = chat_id});
                return true;
            } catch (Exception e) {
                Console.WriteLine($"user adding to chat error: {e.Message}");
                return false;
            }   
        }
    }
    public void Dispose() {
        dapperContext.DbConnection.Dispose();
    }
}
using Dapper;
using Server.Database.Models;

namespace Server.Database.Repositories;

class ChatRepository: IDisposable {
    private readonly DapperContext dapperContext;
    public ChatRepository() {
        dapperContext = new DapperContext();
    }
    public List<Chat> GetChats() {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            try {
                string query = "SELECT * FROM chats";
                return conn.Query<Chat>(query).ToList();         
            } catch (Exception e) {
                Console.WriteLine($"Getting chats error: {e.Message}");
                return new List<Chat>(){};
            }
        }
    }
    public bool AddUser(int user_id, int chat_id) {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            using (var transaction = conn.BeginTransaction()) {
                try {
                    string checkQuery = "SELECT COUNT(*) FROM chat_members WHERE user_id = @user_id AND chat_id = @chat_id";
                    bool avilable = conn.ExecuteScalar<int>(checkQuery, new {user_id = user_id, chat_id = chat_id}, transaction) == 0;
                    if (avilable) {
                        string addQuery = "INSERT INTO chat_members(user_id, chat_id) VALUES (@user_id, @chat_id)";
                        conn.Execute(addQuery, new {user_id = user_id, chat_id = chat_id}, transaction);
                        transaction.Commit();   
                        return true;
                    }
                    return false;
                } catch (Exception e) {
                    Console.WriteLine($"user adding to chat error: {e.Message}");
                    transaction.Rollback();
                    return false;
                }    
            }  
        }
    }
    public bool KickUser(int user_id, int chat_id) {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            using (var transaction = conn.BeginTransaction()) {
                try {
                    string checkQuery = "SELECT COUNT(*) FROM chat_members WHERE user_id = @user_id AND chat_id = @chat_id";
                    bool avilable = conn.ExecuteScalar<int>(checkQuery, new {user_id = user_id, chat_id = chat_id}, transaction) > 0;
                    if (avilable) {
                        string removeQuery = "DELETE FROM chat_members WHERE user_id = @user_id AND chat_id = @chat_id";
                        conn.Execute(removeQuery, new {user_id = user_id, chat_id = chat_id}, transaction);
                        transaction.Commit();   
                        return true;
                    }
                    return false;
                } catch (Exception e) {
                    Console.WriteLine($"user adding to chat error: {e.Message}");
                    transaction.Rollback();
                    return false;
                }    
            }
        }
    }
    public bool ChangeChat(int chat_id, string key, object value) {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            try {
                string query = "UPDATE chats SET @key = @value WHERE chat_id = @chat_id";
                conn.Execute(query, new {chat_id = chat_id, key = key, value = value});
                return true;
            } catch (Exception e) {
                Console.WriteLine($"Changing chat error: {e.Message}");
                return false;
            }
        }
    }
    public void Dispose() {
        dapperContext.DbConnection.Dispose();
    }
}
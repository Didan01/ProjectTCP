using Dapper;
using Server.Database.Models;

namespace Server.Database.Repositories;

class UserRepository: IDisposable {
    private readonly DapperContext dapperContext;
    public UserRepository() {
        dapperContext = new DapperContext();
    }
    public List<User> GetUsers() {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            string query = "SELECT * FROM users";
            return conn.Query<User>(query).ToList();      
        }
    }
    public User Login(User loginUser) {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            try {
                string query = "SELECT * FROM users s WHERE s.user_name = @user_name AND s.password = @password";
                return conn.QueryFirstOrDefault<User>(query, loginUser);   
            } catch (Exception e) {
                Console.WriteLine($"login error: {e.Message}");
                return null;
            }
        }    
    }
    public bool Register(User registerUser) {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            using (var transaction = conn.BeginTransaction()) {
                try {
                    string checkQuery = "SELECT COUNT(*) FROM users s WHERE s.user_name = @user_name";
                    bool isFree = conn.ExecuteScalar<int>(checkQuery, registerUser, transaction) == 0;
                    if (isFree) {
                        string registerQuery = "INSERT INTO users(user_name, password) VALUES (@user_name, @password)";
                        conn.Execute(registerQuery, registerUser, transaction);
                        transaction.Commit();
                        return true;
                    }   
                    return false;
                } catch (Exception e) {
                    Console.WriteLine($"register error: {e.Message}");
                    transaction.Rollback();
                    return false;
                }
            }   
        }
    }
    public bool SendMessage(int chat_id, Messsage message) {
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            using (var transaction = conn.BeginTransaction()) {
                try {
                    string addMessageQuery = "INSERT INTO messages(send_time, body, sender_id) VALUES (@send_time, @body, @sender_id) RETURNING addition_id";
                    int message_id = conn.ExecuteScalar<int>(addMessageQuery, message, transaction);
                    string attachMessageQuery = "INSERT INTO chat_messages(message_id, chat_id) VALUES (@chat_id, @message_id)";
                    conn.Execute(attachMessageQuery, new {chat_id = chat_id, message_id = message_id}, transaction);
                    transaction.Commit();
                    return true;
                } catch (Exception e) {
                    Console.WriteLine($"sending message error: {e.Message}");
                    transaction.Rollback();
                    return false;
                }
            }   
        }
    }
    public int CreateChat(Chat chatInfo) {
        // в случаи ошибки вернет -1
        using (var conn = dapperContext.DbConnection) {
            conn.Open();
            try {
                string query = "INSERT INTO chats(chat_name) VALUES (@chat_name) RETURNING chat_id";
                int chat_id = conn.ExecuteScalar<int>(query, chatInfo);
                return chat_id;
            } catch (Exception e) {
                Console.WriteLine($"creating chat error: {e.Message}");
                return -1;
            }   
        }
    }
    public void Dispose() {
        dapperContext.DbConnection.Dispose();
    }
}
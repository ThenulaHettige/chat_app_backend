using FormulaOne.ChatService.DataService;
using FormulaOne.ChatService.Models;
using Microsoft.AspNetCore.SignalR;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace FormulaOne.ChatService.Hubs
{
    public class ChatHub : Hub
    {
        private readonly SharedDb _shared;
        private readonly MySqlConnection _dbConnection;

        public ChatHub(SharedDb shared, MySqlConnection dbConnection)
        {
            _shared = shared;
            _dbConnection = dbConnection;
        }

        public async Task JoinChat(UserConnection conn)
        {
            await Clients.All
                .SendAsync("ReceiveMessage", "admin", $"{conn.Username} has joined");
        }

        private async Task EnsureConnectionOpenAsync()
        {
            if (_dbConnection.State != System.Data.ConnectionState.Open)
            {
                await _dbConnection.OpenAsync();
            }
        }

        public async Task JoinSpecificChatRoom(UserConnection conn)
        {
            await EnsureConnectionOpenAsync();

            // Check if room exists in the database
            string queryRoom = "SELECT room_id FROM Room WHERE room_name = @RoomName";
            MySqlCommand cmdRoom = new MySqlCommand(queryRoom, _dbConnection);
            cmdRoom.Parameters.AddWithValue("@RoomName", conn.ChatRoom);

            var roomId = await cmdRoom.ExecuteScalarAsync();

            if (roomId == null)
            {
                string insertRoom = "INSERT INTO Room (room_name, room_connection) VALUES (@RoomName, @RoomConnection)";
                MySqlCommand insertCmd = new MySqlCommand(insertRoom, _dbConnection);
                insertCmd.Parameters.AddWithValue("@RoomName", conn.ChatRoom);
                insertCmd.Parameters.AddWithValue("@RoomConnection", Context.ConnectionId);
                await insertCmd.ExecuteNonQueryAsync();
                roomId = insertCmd.LastInsertedId;
            }

            // Add user to the database if not exists
            string queryUser = "SELECT user_id FROM User WHERE user_name = @Username";
            MySqlCommand cmdUser = new MySqlCommand(queryUser, _dbConnection);
            cmdUser.Parameters.AddWithValue("@Username", conn.Username);

            var userId = await cmdUser.ExecuteScalarAsync();

            if (userId == null)
            {
                string insertUser = "INSERT INTO User (user_name, room_id) VALUES (@Username, @RoomId)";
                MySqlCommand insertUserCmd = new MySqlCommand(insertUser, _dbConnection);
                insertUserCmd.Parameters.AddWithValue("@Username", conn.Username);
                insertUserCmd.Parameters.AddWithValue("@RoomId", roomId);
                await insertUserCmd.ExecuteNonQueryAsync();
                userId = insertUserCmd.LastInsertedId;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, conn.ChatRoom);
            _shared.connections[Context.ConnectionId] = conn;

            await Clients.Group(conn.ChatRoom)
                .SendAsync("ReceiveMessage", "admin", $"{conn.Username} has joined {conn.ChatRoom}");
        }


//        public async Task<object> CreateNewRoom(UserConnection conn)
//        {
//            // Return an anonymous object with named properties
//            return new { Username = conn.Username, ChatRoom = conn.ChatRoom };
//        }


        public async Task CreateNewRoom(UserConnection conn)
        {
            await EnsureConnectionOpenAsync();

            // Check if room exists in the database
            string queryRoom = "SELECT room_id FROM Room WHERE room_name = @RoomName";
            MySqlCommand cmdRoom = new MySqlCommand(queryRoom, _dbConnection);
            cmdRoom.Parameters.AddWithValue("@RoomName", conn.ChatRoom);

            var roomId = await cmdRoom.ExecuteScalarAsync();

            if (roomId == null)
            {
                string insertRoom = "INSERT INTO Room (room_name, room_connection) VALUES (@RoomName, @RoomConnection)";
                MySqlCommand insertCmd = new MySqlCommand(insertRoom, _dbConnection);
                insertCmd.Parameters.AddWithValue("@RoomName", conn.ChatRoom);
                insertCmd.Parameters.AddWithValue("@RoomConnection", Context.ConnectionId);
                await insertCmd.ExecuteNonQueryAsync();
                roomId = insertCmd.LastInsertedId;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, conn.ChatRoom);
            _shared.connections[Context.ConnectionId] = conn;

            await Clients.Group(conn.ChatRoom)
                .SendAsync("ReceiveMessage", "admin", $"{conn.Username} has joined {conn.ChatRoom}");
        }


        public async Task<List<string>> FetchUserChatRooms(string username)
        {
            await EnsureConnectionOpenAsync();

            string query =
                "SELECT DISTINCT r.room_name FROM message m JOIN user u ON m.user_id = u.user_id JOIN room r ON m.room_id = r.room_id WHERE u.user_name = @Username";
            MySqlCommand cmd = new MySqlCommand(query, _dbConnection);
            cmd.Parameters.AddWithValue("@Username", username);

            List<string> chatrooms = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                chatrooms.Add(reader.GetString("room_name"));
            }

            return chatrooms;
        }

        public async Task SendMessage(string msg)
        {
            await EnsureConnectionOpenAsync();

            if (_shared.connections.TryGetValue(Context.ConnectionId, out UserConnection conn))
            {
                string queryUser = "SELECT user_id FROM User WHERE user_name = @Username";
                MySqlCommand cmdUser = new MySqlCommand(queryUser, _dbConnection);
                cmdUser.Parameters.AddWithValue("@Username", conn.Username);

                var userId = await cmdUser.ExecuteScalarAsync();

                string queryRoom = "SELECT room_id FROM Room WHERE room_name = @RoomName";
                MySqlCommand cmdRoom = new MySqlCommand(queryRoom, _dbConnection);
                cmdRoom.Parameters.AddWithValue("@RoomName", conn.ChatRoom);

                var roomId = await cmdRoom.ExecuteScalarAsync();

                string insertMessage =
                    "INSERT INTO Message (room_id, user_id, message) VALUES (@RoomId, @UserId, @Message)";
                MySqlCommand insertCmd = new MySqlCommand(insertMessage, _dbConnection);
                insertCmd.Parameters.AddWithValue("@RoomId", roomId);
                insertCmd.Parameters.AddWithValue("@UserId", userId);
                insertCmd.Parameters.AddWithValue("@Message", msg);
                await insertCmd.ExecuteNonQueryAsync();

                await Clients.Group(conn.ChatRoom)
                    .SendAsync("ReceiveMessage", conn.Username, msg);
            }
        }

        public async Task<string> SignUpUser(string firstname, string lastname, string email, string username)
        {
            await EnsureConnectionOpenAsync();
            try
            {
                string insertUserQuery =
                    "INSERT INTO user (first_name, last_name, email, user_name, room_id) VALUES (@Firstname, @Lastname, @Email, @Username, NULL)";
                MySqlCommand insertUserCmd = new MySqlCommand(insertUserQuery, _dbConnection);

                insertUserCmd.Parameters.AddWithValue("@Firstname", firstname);
                insertUserCmd.Parameters.AddWithValue("@Lastname", lastname);
                insertUserCmd.Parameters.AddWithValue("@Email", email);
                insertUserCmd.Parameters.AddWithValue("@Username", username);

                await insertUserCmd.ExecuteNonQueryAsync();

                return "User registered successfully.";
            }
            catch (MySqlException ex)
            {
                // Handle duplicate email error (MySQL error code 1062 for duplicate key)
                if (ex.Number == 1062)
                {
                    return "Error: A user with this email already exists.";
                }
                else
                {
                    return $"Error: {ex.Message}";
                }
            }
        }


        public async Task FetchHistory(string room_name)
        {
            await EnsureConnectionOpenAsync();

            string queryRoom = "SELECT room_id FROM Room WHERE room_name = @RoomName";
            MySqlCommand cmdRoom = new MySqlCommand(queryRoom, _dbConnection);
            cmdRoom.Parameters.AddWithValue("@RoomName", room_name);

            var roomIdObj = await cmdRoom.ExecuteScalarAsync();
            object roomId = roomIdObj ?? DBNull.Value;

            if (roomId != DBNull.Value && int.TryParse(roomId.ToString(), out int roomIdInt))
            {
                string queryMessages =
                    "SELECT u.user_name, m.message FROM User u JOIN Message m ON u.user_id = m.user_id JOIN Room r ON m.room_id = r.room_id WHERE r.room_id = @RoomId";
                MySqlCommand cmdMessages = new MySqlCommand(queryMessages, _dbConnection);
                cmdMessages.Parameters.AddWithValue("@RoomId", roomIdInt);

                using var reader = await cmdMessages.ExecuteReaderAsync();

                var messages = new List<object>();

                while (await reader.ReadAsync())
                {
                    var username = reader.GetString("user_name");
                    var message = reader.GetString("message");

                    var messageData = new
                    {
                        Username = username,
                        Message = message
                    };

                    messages.Add(messageData);
                }

                await Clients.Caller.SendAsync("ReceiveHistory", messages);
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveHistory", "admin", $"Room '{room_name}' does not exist.");
            }
        }
    }
}
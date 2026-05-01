DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS messages CASCADE;
DROP TABLE IF EXISTS chats CASCADE;
DROP TABLE IF EXISTS chat_members CASCADE;
DROP TABLE IF EXISTS chat_messages CASCADE;

CREATE TABLE users (
	user_id SERIAL PRIMARY KEY,
	user_name VARCHAR(30),
	password VARCHAR(30)
);

CREATE TABLE messages (
	message_id SERIAL PRIMARY KEY,
	send_time VARCHAR(100),
	body VARCHAR(200),
	sender_id INT REFERENCES users(user_id)
);

CREATE TABLE chats (
	chat_id SERIAL PRIMARY KEY,
	chat_name VARCHAR(100)
);

CREATE TABLE chat_members (
	membership_id SERIAL PRIMARY KEY,
	user_id INT REFERENCES users(user_id),
	chat_id INT REFERENCES chats(chat_id)
);

CREATE TABLE chat_messages (
	addition_id SERIAL PRIMARY KEY,
	message_id INT REFERENCES messages(message_id),
	chat_id INT REFERENCES chats(chat_id)
);
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Characters (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL,
    Personality TEXT NOT NULL,
    Scenario TEXT NOT NULL,
    FirstMessage TEXT NOT NULL,
    ExampleMessages TEXT NOT NULL,
    Creator TEXT NOT NULL,
    Tags TEXT NOT NULL,
    IsFavorite INTEGER NOT NULL DEFAULT 0,
    AvatarPath TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Characters_Name ON Characters(Name);
CREATE TABLE IF NOT EXISTS ChatSessions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    CharacterId INTEGER NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS ChatMessages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChatSessionId INTEGER NOT NULL,
    Role TEXT NOT NULL,
    Content TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NULL,
    FOREIGN KEY (ChatSessionId) REFERENCES ChatSessions(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_ChatMessages_ChatSessionId ON ChatMessages(ChatSessionId);
CREATE INDEX IF NOT EXISTS IX_ChatSessions_CharacterId ON ChatSessions(CharacterId);
CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);

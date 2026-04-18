BEGIN TRANSACTION;
ALTER TABLE [Players] ADD [PasswordHash] nvarchar(max) NULL;

ALTER TABLE [Players] ADD [RefreshToken] nvarchar(max) NULL;

ALTER TABLE [Players] ADD [RefreshTokenExpiry] datetime2 NULL;

ALTER TABLE [GameSessions] ADD [PlayerId1] uniqueidentifier NULL;

CREATE INDEX [IX_GameSessions_PlayerId1] ON [GameSessions] ([PlayerId1]);

ALTER TABLE [GameSessions] ADD CONSTRAINT [FK_GameSessions_Players_PlayerId1] FOREIGN KEY ([PlayerId1]) REFERENCES [Players] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260418034217_AddPlayerAuthFields', N'10.0.5');

COMMIT;
GO


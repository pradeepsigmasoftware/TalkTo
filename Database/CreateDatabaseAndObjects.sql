-- ============================================================================
-- Database Creation Script: TawkToLiveChatDb
-- Tawk.to-Style Live Chat Application
-- ============================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'TawkToLiveChatDb')
BEGIN
    CREATE DATABASE TawkToLiveChatDb;
END
GO

USE TawkToLiveChatDb;
GO

-- 1. Drop Foreign Keys & Tables if they exist (for clean re-run)
IF OBJECT_ID('dbo.Chat_Ratings', 'U') IS NOT NULL DROP TABLE dbo.Chat_Ratings;
IF OBJECT_ID('dbo.Chat_InternalNotes', 'U') IS NOT NULL DROP TABLE dbo.Chat_InternalNotes;
IF OBJECT_ID('dbo.Chat_Attachments', 'U') IS NOT NULL DROP TABLE dbo.Chat_Attachments;
IF OBJECT_ID('dbo.Chat_Messages', 'U') IS NOT NULL DROP TABLE dbo.Chat_Messages;
IF OBJECT_ID('dbo.Chat_Assignments', 'U') IS NOT NULL DROP TABLE dbo.Chat_Assignments;
IF OBJECT_ID('dbo.Chat_Sessions', 'U') IS NOT NULL DROP TABLE dbo.Chat_Sessions;
IF OBJECT_ID('dbo.Chat_AgentServices', 'U') IS NOT NULL DROP TABLE dbo.Chat_AgentServices;
IF OBJECT_ID('dbo.Chat_Agents', 'U') IS NOT NULL DROP TABLE dbo.Chat_Agents;
IF OBJECT_ID('dbo.Chat_Services', 'U') IS NOT NULL DROP TABLE dbo.Chat_Services;
IF OBJECT_ID('dbo.Chat_Visitors', 'U') IS NOT NULL DROP TABLE dbo.Chat_Visitors;
GO

-- ============================================================================
-- 1. Chat_Visitors Table
-- ============================================================================
CREATE TABLE dbo.Chat_Visitors (
    VisitorId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CountryCode NVARCHAR(10) NOT NULL CONSTRAINT DF_Chat_Visitors_CountryCode DEFAULT ('+91'),
    MobileNumber NVARCHAR(20) NOT NULL,
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_Visitors_CreatedDate DEFAULT (SYSUTCDATETIME())
);
CREATE NONCLUSTERED INDEX IX_Chat_Visitors_MobileNumber ON dbo.Chat_Visitors (MobileNumber);
GO

-- ============================================================================
-- 2. Chat_Services Table
-- ============================================================================
CREATE TABLE dbo.Chat_Services (
    ServiceId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ServiceName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    DisplayOrder INT NOT NULL CONSTRAINT DF_Chat_Services_DisplayOrder DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Chat_Services_IsActive DEFAULT (1)
);
CREATE NONCLUSTERED INDEX IX_Chat_Services_DisplayOrder ON dbo.Chat_Services (DisplayOrder, IsActive);
GO

-- ============================================================================
-- 3. Chat_Agents Table
-- ============================================================================
CREATE TABLE dbo.Chat_Agents (
    AgentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AgentName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(150) NOT NULL CONSTRAINT UQ_Chat_Agents_Email UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    ProfileImage NVARCHAR(255) NULL,
    AvailabilityStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_Chat_Agents_Status DEFAULT ('Offline'), -- 'Online', 'Offline', 'Busy'
    MaxConcurrentChats INT NOT NULL CONSTRAINT DF_Chat_Agents_MaxConcurrent DEFAULT (5),
    Role NVARCHAR(20) NOT NULL CONSTRAINT DF_Chat_Agents_Role DEFAULT ('Agent'), -- 'Admin', 'Agent'
    IsActive BIT NOT NULL CONSTRAINT DF_Chat_Agents_IsActive DEFAULT (1),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_Agents_CreatedDate DEFAULT (SYSUTCDATETIME())
);
CREATE NONCLUSTERED INDEX IX_Chat_Agents_Availability ON dbo.Chat_Agents (AvailabilityStatus, IsActive);
GO

-- ============================================================================
-- 4. Chat_AgentServices Table
-- ============================================================================
CREATE TABLE dbo.Chat_AgentServices (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AgentId INT NOT NULL CONSTRAINT FK_Chat_AgentServices_Agent FOREIGN KEY REFERENCES dbo.Chat_Agents(AgentId) ON DELETE CASCADE,
    ServiceId INT NOT NULL CONSTRAINT FK_Chat_AgentServices_Service FOREIGN KEY REFERENCES dbo.Chat_Services(ServiceId) ON DELETE CASCADE
);
CREATE UNIQUE NONCLUSTERED INDEX IX_Chat_AgentServices_Unique ON dbo.Chat_AgentServices (AgentId, ServiceId);
GO

-- ============================================================================
-- 5. Chat_Sessions Table
-- ============================================================================
CREATE TABLE dbo.Chat_Sessions (
    ChatSessionId NVARCHAR(50) NOT NULL PRIMARY KEY,
    VisitorId INT NOT NULL CONSTRAINT FK_Chat_Sessions_Visitor FOREIGN KEY REFERENCES dbo.Chat_Visitors(VisitorId),
    ServiceId INT NOT NULL CONSTRAINT FK_Chat_Sessions_Service FOREIGN KEY REFERENCES dbo.Chat_Services(ServiceId),
    AssignedAgentId INT NULL CONSTRAINT FK_Chat_Sessions_Agent FOREIGN KEY REFERENCES dbo.Chat_Agents(AgentId),
    SessionStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_Chat_Sessions_Status DEFAULT ('Pending'), -- 'Pending', 'Active', 'Completed', 'Closed'
    StartDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_Sessions_StartDate DEFAULT (SYSUTCDATETIME()),
    EndDate DATETIME2 NULL
);
CREATE NONCLUSTERED INDEX IX_Chat_Sessions_VisitorId ON dbo.Chat_Sessions (VisitorId);
CREATE NONCLUSTERED INDEX IX_Chat_Sessions_ServiceId ON dbo.Chat_Sessions (ServiceId);
CREATE NONCLUSTERED INDEX IX_Chat_Sessions_AgentId ON dbo.Chat_Sessions (AssignedAgentId, SessionStatus);
CREATE NONCLUSTERED INDEX IX_Chat_Sessions_Status_Date ON dbo.Chat_Sessions (SessionStatus, StartDate);
GO

-- ============================================================================
-- 6. Chat_Messages Table
-- ============================================================================
CREATE TABLE dbo.Chat_Messages (
    MessageId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ChatSessionId NVARCHAR(50) NOT NULL CONSTRAINT FK_Chat_Messages_Session FOREIGN KEY REFERENCES dbo.Chat_Sessions(ChatSessionId) ON DELETE CASCADE,
    SenderId NVARCHAR(100) NOT NULL,
    SenderType NVARCHAR(20) NOT NULL, -- 'Customer', 'Agent', 'System'
    MessageText NVARCHAR(MAX) NOT NULL,
    MessageType NVARCHAR(20) NOT NULL CONSTRAINT DF_Chat_Messages_Type DEFAULT ('Text'), -- 'Text', 'Image', 'File'
    SentDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_Messages_SentDate DEFAULT (SYSUTCDATETIME()),
    IsRead BIT NOT NULL CONSTRAINT DF_Chat_Messages_IsRead DEFAULT (0)
);
CREATE NONCLUSTERED INDEX IX_Chat_Messages_SessionId ON dbo.Chat_Messages (ChatSessionId, SentDate ASC);
GO

-- ============================================================================
-- 7. Chat_Assignments Table
-- ============================================================================
CREATE TABLE dbo.Chat_Assignments (
    AssignmentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ChatSessionId NVARCHAR(50) NOT NULL CONSTRAINT FK_Chat_Assignments_Session FOREIGN KEY REFERENCES dbo.Chat_Sessions(ChatSessionId) ON DELETE CASCADE,
    AgentId INT NOT NULL CONSTRAINT FK_Chat_Assignments_Agent FOREIGN KEY REFERENCES dbo.Chat_Agents(AgentId),
    AssignedDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_Assignments_AssignedDate DEFAULT (SYSUTCDATETIME()),
    AssignedBy NVARCHAR(50) NOT NULL CONSTRAINT DF_Chat_Assignments_AssignedBy DEFAULT ('System')
);
CREATE NONCLUSTERED INDEX IX_Chat_Assignments_Session ON dbo.Chat_Assignments (ChatSessionId);
GO

-- ============================================================================
-- 8. Chat_Attachments Table
-- ============================================================================
CREATE TABLE dbo.Chat_Attachments (
    AttachmentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MessageId INT NOT NULL CONSTRAINT FK_Chat_Attachments_Message FOREIGN KEY REFERENCES dbo.Chat_Messages(MessageId) ON DELETE CASCADE,
    FileName NVARCHAR(255) NOT NULL,
    FilePath NVARCHAR(500) NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    UploadedDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_Attachments_UploadedDate DEFAULT (SYSUTCDATETIME())
);
CREATE NONCLUSTERED INDEX IX_Chat_Attachments_Message ON dbo.Chat_Attachments (MessageId);
GO

-- ============================================================================
-- 9. Chat_InternalNotes Table
-- ============================================================================
CREATE TABLE dbo.Chat_InternalNotes (
    NoteId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ChatSessionId NVARCHAR(50) NOT NULL CONSTRAINT FK_Chat_InternalNotes_Session FOREIGN KEY REFERENCES dbo.Chat_Sessions(ChatSessionId) ON DELETE CASCADE,
    AgentId INT NOT NULL CONSTRAINT FK_Chat_InternalNotes_Agent FOREIGN KEY REFERENCES dbo.Chat_Agents(AgentId),
    NoteText NVARCHAR(MAX) NOT NULL,
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_InternalNotes_CreatedDate DEFAULT (SYSUTCDATETIME())
);
CREATE NONCLUSTERED INDEX IX_Chat_InternalNotes_Session ON dbo.Chat_InternalNotes (ChatSessionId, CreatedDate DESC);
GO

-- ============================================================================
-- 10. Chat_Ratings Table
-- ============================================================================
CREATE TABLE dbo.Chat_Ratings (
    RatingId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ChatSessionId NVARCHAR(50) NOT NULL CONSTRAINT FK_Chat_Ratings_Session FOREIGN KEY REFERENCES dbo.Chat_Sessions(ChatSessionId) ON DELETE CASCADE,
    Rating INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
    Feedback NVARCHAR(1000) NULL,
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Chat_Ratings_CreatedDate DEFAULT (SYSUTCDATETIME())
);
CREATE NONCLUSTERED INDEX IX_Chat_Ratings_Session ON dbo.Chat_Ratings (ChatSessionId);
GO

-- ============================================================================
-- Seed Default Services (Exactly 9 as specified in prompt)
-- ============================================================================
INSERT INTO dbo.Chat_Services (ServiceName, Description, DisplayOrder, IsActive)
VALUES 
(N'Technical Support', N'Assistance with software issues, bugs, and technical troubleshooting', 1, 1),
(N'Website Development', N'Custom web application development, design, and revamp queries', 2, 1),
(N'Software Development', N'Enterprise software development, APIs, and backend solutions', 3, 1),
(N'Payment Related Query', N'Billing questions, invoice queries, and payment processing assistance', 4, 1),
(N'Product Enquiry', N'Product features, demos, specifications, and walkthroughs', 5, 1),
(N'Sales & Marketing', N'Pricing, custom quotes, partnerships, and business opportunities', 6, 1),
(N'Account Related Query', N'User profile, credentials, security, and account settings', 7, 1),
(N'General Enquiry', N'General questions, business hours, and general guidance', 8, 1),
(N'Other', N'Any other questions or feedback not listed above', 9, 1);
GO

-- ============================================================================
-- Seed Initial Agents & Admin (Passwords hashed using SHA-256 for: Admin@123 / Agent@123)
-- SHA-256 of 'Admin@123' = 'e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7'
-- SHA-256 of 'Agent@123' = 'c3206ca954a4953c1c1b82b4167bd097deab0e228207e3b3296237e369f39d34'
-- ============================================================================
INSERT INTO dbo.Chat_Agents (AgentName, Email, PasswordHash, ProfileImage, AvailabilityStatus, MaxConcurrentChats, Role, IsActive)
VALUES 
(N'Administrator', N'admin@tawkto.local', N'e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7', N'/images/admin-avatar.svg', N'Online', 10, N'Admin', 1),
(N'Rahul Sharma', N'rahul.tech@tawkto.local', N'c3206ca954a4953c1c1b82b4167bd097deab0e228207e3b3296237e369f39d34', N'/images/agent-rahul.svg', N'Online', 5, N'Agent', 1),
(N'Priya Patel', N'priya.sales@tawkto.local', N'c3206ca954a4953c1c1b82b4167bd097deab0e228207e3b3296237e369f39d34', N'/images/agent-priya.svg', N'Online', 5, N'Agent', 1),
(N'Amit Kumar', N'amit.support@tawkto.local', N'c3206ca954a4953c1c1b82b4167bd097deab0e228207e3b3296237e369f39d34', N'/images/agent-amit.svg', N'Online', 5, N'Agent', 1);
GO

-- Map Agents to Services:
-- Rahul (AgentId 2): Technical Support (1), Website Dev (2), Software Dev (3)
-- Priya (AgentId 3): Payment Related Query (4), Product Enquiry (5), Sales & Marketing (6)
-- Amit (AgentId 4): Account Related Query (7), General Enquiry (8), Other (9)
-- Admin (AgentId 1): All Services (1-9)
INSERT INTO dbo.Chat_AgentServices (AgentId, ServiceId)
SELECT 1, ServiceId FROM dbo.Chat_Services;

INSERT INTO dbo.Chat_AgentServices (AgentId, ServiceId) VALUES
(2, 1), (2, 2), (2, 3),
(3, 4), (3, 5), (3, 6),
(4, 7), (4, 8), (4, 9);
GO

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

-- 1. sp_GetActiveServices
CREATE OR ALTER PROCEDURE dbo.sp_GetActiveServices
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ServiceId, ServiceName, Description, DisplayOrder, IsActive
    FROM dbo.Chat_Services
    WHERE IsActive = 1
    ORDER BY DisplayOrder ASC, ServiceName ASC;
END;
GO

-- 2. sp_GetAllServices
CREATE OR ALTER PROCEDURE dbo.sp_GetAllServices
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        s.ServiceId, 
        s.ServiceName, 
        s.Description, 
        s.DisplayOrder, 
        s.IsActive,
        (SELECT COUNT(1) FROM dbo.Chat_AgentServices cas WHERE cas.ServiceId = s.ServiceId) AS AssignedAgentsCount,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions cs WHERE cs.ServiceId = s.ServiceId) AS TotalSessionsCount
    FROM dbo.Chat_Services s
    ORDER BY s.DisplayOrder ASC, s.ServiceName ASC;
END;
GO

-- 3. sp_SaveService (Create or Update)
CREATE OR ALTER PROCEDURE dbo.sp_SaveService
    @ServiceId INT,
    @ServiceName NVARCHAR(100),
    @Description NVARCHAR(500),
    @DisplayOrder INT,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @ServiceId <= 0
    BEGIN
        INSERT INTO dbo.Chat_Services (ServiceName, Description, DisplayOrder, IsActive)
        VALUES (@ServiceName, @Description, @DisplayOrder, @IsActive);
        SELECT SCOPE_IDENTITY() AS ServiceId;
    END
    ELSE
    BEGIN
        UPDATE dbo.Chat_Services
        SET ServiceName = @ServiceName,
            Description = @Description,
            DisplayOrder = @DisplayOrder,
            IsActive = @IsActive
        WHERE ServiceId = @ServiceId;
        SELECT @ServiceId AS ServiceId;
    END
END;
GO

-- 4. sp_FindOrCreateVisitor
CREATE OR ALTER PROCEDURE dbo.sp_FindOrCreateVisitor
    @CountryCode NVARCHAR(10),
    @MobileNumber NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @VisitorId INT;

    SELECT TOP 1 @VisitorId = VisitorId
    FROM dbo.Chat_Visitors
    WHERE MobileNumber = @MobileNumber AND CountryCode = @CountryCode;

    IF @VisitorId IS NULL
    BEGIN
        INSERT INTO dbo.Chat_Visitors (CountryCode, MobileNumber, CreatedDate)
        VALUES (@CountryCode, @MobileNumber, SYSUTCDATETIME());

        SET @VisitorId = SCOPE_IDENTITY();
    END

    SELECT VisitorId, CountryCode, MobileNumber, CreatedDate
    FROM dbo.Chat_Visitors
    WHERE VisitorId = @VisitorId;
END;
GO

-- 5. sp_FindAvailableAgentForService
CREATE OR ALTER PROCEDURE dbo.sp_FindAvailableAgentForService
    @ServiceId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Pick the online agent assigned to this service who has not reached MaxConcurrentChats,
    -- prioritized by the fewest currently active chats (load balancing)
    SELECT TOP 1 
        a.AgentId,
        a.AgentName,
        a.Email,
        a.ProfileImage,
        a.AvailabilityStatus,
        a.MaxConcurrentChats,
        a.Role,
        COUNT(cs.ChatSessionId) AS CurrentActiveChats
    FROM dbo.Chat_Agents a
    INNER JOIN dbo.Chat_AgentServices cas ON a.AgentId = cas.AgentId
    LEFT JOIN dbo.Chat_Sessions cs ON a.AgentId = cs.AssignedAgentId AND cs.SessionStatus = 'Active'
    WHERE cas.ServiceId = @ServiceId
      AND a.IsActive = 1
      AND a.AvailabilityStatus = 'Online'
    GROUP BY a.AgentId, a.AgentName, a.Email, a.ProfileImage, a.AvailabilityStatus, a.MaxConcurrentChats, a.Role
    HAVING COUNT(cs.ChatSessionId) < a.MaxConcurrentChats
    ORDER BY CurrentActiveChats ASC, a.AgentId ASC;
END;
GO

-- 6. sp_CreateChatSession
CREATE OR ALTER PROCEDURE dbo.sp_CreateChatSession
    @ChatSessionId NVARCHAR(50),
    @VisitorId INT,
    @ServiceId INT,
    @AssignedAgentId INT NULL,
    @SessionStatus NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Chat_Sessions (ChatSessionId, VisitorId, ServiceId, AssignedAgentId, SessionStatus, StartDate)
    VALUES (@ChatSessionId, @VisitorId, @ServiceId, @AssignedAgentId, @SessionStatus, SYSUTCDATETIME());

    IF @AssignedAgentId IS NOT NULL
    BEGIN
        INSERT INTO dbo.Chat_Assignments (ChatSessionId, AgentId, AssignedDate, AssignedBy)
        VALUES (@ChatSessionId, @AssignedAgentId, SYSUTCDATETIME(), 'System');
    END

    -- Return session details with joins
    EXEC dbo.sp_GetChatSessionById @ChatSessionId = @ChatSessionId;
END;
GO

-- 7. sp_GetChatSessionById
CREATE OR ALTER PROCEDURE dbo.sp_GetChatSessionById
    @ChatSessionId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        s.ChatSessionId,
        s.VisitorId,
        v.CountryCode,
        v.MobileNumber,
        s.ServiceId,
        srv.ServiceName,
        s.AssignedAgentId,
        a.AgentName,
        a.Email AS AgentEmail,
        a.ProfileImage AS AgentProfileImage,
        a.AvailabilityStatus AS AgentStatus,
        s.SessionStatus,
        s.StartDate,
        s.EndDate,
        (SELECT COUNT(1) FROM dbo.Chat_Messages m WHERE m.ChatSessionId = s.ChatSessionId) AS TotalMessages,
        (SELECT COUNT(1) FROM dbo.Chat_Messages m WHERE m.ChatSessionId = s.ChatSessionId AND m.IsRead = 0 AND m.SenderType = 'Customer') AS UnreadCustomerMessages
    FROM dbo.Chat_Sessions s
    INNER JOIN dbo.Chat_Visitors v ON s.VisitorId = v.VisitorId
    INNER JOIN dbo.Chat_Services srv ON s.ServiceId = srv.ServiceId
    LEFT JOIN dbo.Chat_Agents a ON s.AssignedAgentId = a.AgentId
    WHERE s.ChatSessionId = @ChatSessionId;
END;
GO

-- 8. sp_AssignAgentToSession
CREATE OR ALTER PROCEDURE dbo.sp_AssignAgentToSession
    @ChatSessionId NVARCHAR(50),
    @AgentId INT,
    @AssignedBy NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Chat_Sessions
    SET AssignedAgentId = @AgentId,
        SessionStatus = 'Active'
    WHERE ChatSessionId = @ChatSessionId;

    INSERT INTO dbo.Chat_Assignments (ChatSessionId, AgentId, AssignedDate, AssignedBy)
    VALUES (@ChatSessionId, @AgentId, SYSUTCDATETIME(), @AssignedBy);

    EXEC dbo.sp_GetChatSessionById @ChatSessionId = @ChatSessionId;
END;
GO

-- 9. sp_SaveMessage
CREATE OR ALTER PROCEDURE dbo.sp_SaveMessage
    @ChatSessionId NVARCHAR(50),
    @SenderId NVARCHAR(100),
    @SenderType NVARCHAR(20),
    @MessageText NVARCHAR(MAX),
    @MessageType NVARCHAR(20) = 'Text'
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Chat_Messages (ChatSessionId, SenderId, SenderType, MessageText, MessageType, SentDate, IsRead)
    VALUES (@ChatSessionId, @SenderId, @SenderType, @MessageText, @MessageType, SYSUTCDATETIME(), 0);

    DECLARE @MessageId INT = SCOPE_IDENTITY();

    SELECT 
        m.MessageId,
        m.ChatSessionId,
        m.SenderId,
        m.SenderType,
        m.MessageText,
        m.MessageType,
        m.SentDate,
        m.IsRead,
        att.AttachmentId,
        att.FileName,
        att.FilePath,
        att.ContentType
    FROM dbo.Chat_Messages m
    LEFT JOIN dbo.Chat_Attachments att ON m.MessageId = att.MessageId
    WHERE m.MessageId = @MessageId;
END;
GO

-- 10. sp_SaveAttachment
CREATE OR ALTER PROCEDURE dbo.sp_SaveAttachment
    @MessageId INT,
    @FileName NVARCHAR(255),
    @FilePath NVARCHAR(500),
    @ContentType NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Chat_Attachments (MessageId, FileName, FilePath, ContentType, UploadedDate)
    VALUES (@MessageId, @FileName, @FilePath, @ContentType, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS AttachmentId;
END;
GO

-- 11. sp_GetChatMessages
CREATE OR ALTER PROCEDURE dbo.sp_GetChatMessages
    @ChatSessionId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        m.MessageId,
        m.ChatSessionId,
        m.SenderId,
        m.SenderType,
        m.MessageText,
        m.MessageType,
        m.SentDate,
        m.IsRead,
        att.AttachmentId,
        att.FileName,
        att.FilePath,
        att.ContentType
    FROM dbo.Chat_Messages m
    LEFT JOIN dbo.Chat_Attachments att ON m.MessageId = att.MessageId
    WHERE m.ChatSessionId = @ChatSessionId
    ORDER BY m.SentDate ASC, m.MessageId ASC;
END;
GO

-- 12. sp_MarkMessagesAsRead
CREATE OR ALTER PROCEDURE dbo.sp_MarkMessagesAsRead
    @ChatSessionId NVARCHAR(50),
    @ReaderType NVARCHAR(20) -- If 'Agent', marks Customer messages as read; if 'Customer', marks Agent messages as read
AS
BEGIN
    SET NOCOUNT ON;

    IF @ReaderType = 'Agent'
    BEGIN
        UPDATE dbo.Chat_Messages
        SET IsRead = 1
        WHERE ChatSessionId = @ChatSessionId AND SenderType = 'Customer' AND IsRead = 0;
    END
    ELSE IF @ReaderType = 'Customer'
    BEGIN
        UPDATE dbo.Chat_Messages
        SET IsRead = 1
        WHERE ChatSessionId = @ChatSessionId AND SenderType = 'Agent' AND IsRead = 0;
    END
END;
GO

-- 13. sp_CloseChatSession
CREATE OR ALTER PROCEDURE dbo.sp_CloseChatSession
    @ChatSessionId NVARCHAR(50),
    @ClosedBy NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Chat_Sessions
    SET SessionStatus = 'Completed',
        EndDate = SYSUTCDATETIME()
    WHERE ChatSessionId = @ChatSessionId;

    -- Add system message
    INSERT INTO dbo.Chat_Messages (ChatSessionId, SenderId, SenderType, MessageText, MessageType, SentDate, IsRead)
    VALUES (@ChatSessionId, 'System', 'System', CONCAT('This chat session was closed by ', @ClosedBy, '.'), 'Text', SYSUTCDATETIME(), 1);

    EXEC dbo.sp_GetChatSessionById @ChatSessionId = @ChatSessionId;
END;
GO

-- 14. sp_SaveInternalNote
CREATE OR ALTER PROCEDURE dbo.sp_SaveInternalNote
    @ChatSessionId NVARCHAR(50),
    @AgentId INT,
    @NoteText NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Chat_InternalNotes (ChatSessionId, AgentId, NoteText, CreatedDate)
    VALUES (@ChatSessionId, @AgentId, @NoteText, SYSUTCDATETIME());

    SELECT 
        n.NoteId,
        n.ChatSessionId,
        n.AgentId,
        a.AgentName,
        n.NoteText,
        n.CreatedDate
    FROM dbo.Chat_InternalNotes n
    INNER JOIN dbo.Chat_Agents a ON n.AgentId = a.AgentId
    WHERE n.NoteId = SCOPE_IDENTITY();
END;
GO

-- 15. sp_GetInternalNotes
CREATE OR ALTER PROCEDURE dbo.sp_GetInternalNotes
    @ChatSessionId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        n.NoteId,
        n.ChatSessionId,
        n.AgentId,
        a.AgentName,
        n.NoteText,
        n.CreatedDate
    FROM dbo.Chat_InternalNotes n
    INNER JOIN dbo.Chat_Agents a ON n.AgentId = a.AgentId
    WHERE n.ChatSessionId = @ChatSessionId
    ORDER BY n.CreatedDate DESC;
END;
GO

-- 16. sp_SaveRating
CREATE OR ALTER PROCEDURE dbo.sp_SaveRating
    @ChatSessionId NVARCHAR(50),
    @Rating INT,
    @Feedback NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Chat_Ratings (ChatSessionId, Rating, Feedback, CreatedDate)
    VALUES (@ChatSessionId, @Rating, @Feedback, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS RatingId;
END;
GO

-- 17. sp_GetAgentDashboardStats
CREATE OR ALTER PROCEDURE dbo.sp_GetAgentDashboardStats
    @AgentId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(1) FROM dbo.Chat_Sessions WHERE AssignedAgentId = @AgentId) AS TotalAssignedChats,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions WHERE AssignedAgentId = @AgentId AND SessionStatus = 'Active') AS ActiveChats,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions WHERE SessionStatus = 'Pending') AS PendingChats,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions WHERE AssignedAgentId = @AgentId AND SessionStatus IN ('Completed', 'Closed')) AS CompletedChats,
        (SELECT COUNT(1) 
         FROM dbo.Chat_Messages m 
         INNER JOIN dbo.Chat_Sessions s ON m.ChatSessionId = s.ChatSessionId 
         WHERE s.AssignedAgentId = @AgentId AND m.IsRead = 0 AND m.SenderType = 'Customer') AS UnreadMessages;
END;
GO

-- 18. sp_GetAdminDashboardStats
CREATE OR ALTER PROCEDURE dbo.sp_GetAdminDashboardStats
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(1) FROM dbo.Chat_Visitors) AS TotalVisitors,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions) AS TotalChats,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions WHERE SessionStatus = 'Active') AS ActiveChats,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions WHERE SessionStatus = 'Pending') AS PendingChats,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions WHERE SessionStatus IN ('Completed', 'Closed')) AS CompletedChats,
        (SELECT COUNT(1) FROM dbo.Chat_Agents WHERE AvailabilityStatus = 'Online' AND IsActive = 1) AS OnlineAgents,
        (SELECT COUNT(1) FROM dbo.Chat_Agents WHERE AvailabilityStatus = 'Offline' AND IsActive = 1) AS OfflineAgents;

    -- Service-wise breakdown
    SELECT 
        s.ServiceId,
        s.ServiceName,
        COUNT(cs.ChatSessionId) AS TotalChats,
        SUM(CASE WHEN cs.SessionStatus = 'Active' THEN 1 ELSE 0 END) AS ActiveChats,
        SUM(CASE WHEN cs.SessionStatus = 'Pending' THEN 1 ELSE 0 END) AS PendingChats,
        SUM(CASE WHEN cs.SessionStatus IN ('Completed', 'Closed') THEN 1 ELSE 0 END) AS CompletedChats
    FROM dbo.Chat_Services s
    LEFT JOIN dbo.Chat_Sessions cs ON s.ServiceId = cs.ServiceId
    GROUP BY s.ServiceId, s.ServiceName
    ORDER BY TotalChats DESC, s.ServiceName ASC;
END;
GO

-- 19. sp_GetChatSessionsList
CREATE OR ALTER PROCEDURE dbo.sp_GetChatSessionsList
    @AgentId INT = NULL,
    @ServiceId INT = NULL,
    @SessionStatus NVARCHAR(30) = NULL,
    @SearchMobile NVARCHAR(50) = NULL,
    @StartDate DATETIME2 = NULL,
    @EndDate DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        s.ChatSessionId,
        s.VisitorId,
        v.CountryCode,
        v.MobileNumber,
        s.ServiceId,
        srv.ServiceName,
        s.AssignedAgentId,
        a.AgentName,
        a.Email AS AgentEmail,
        a.ProfileImage AS AgentProfileImage,
        a.AvailabilityStatus AS AgentStatus,
        s.SessionStatus,
        s.StartDate,
        s.EndDate,
        (SELECT COUNT(1) FROM dbo.Chat_Messages m WHERE m.ChatSessionId = s.ChatSessionId) AS TotalMessages,
        (SELECT COUNT(1) FROM dbo.Chat_Messages m WHERE m.ChatSessionId = s.ChatSessionId AND m.IsRead = 0 AND m.SenderType = 'Customer') AS UnreadCustomerMessages,
        (SELECT TOP 1 m.MessageText FROM dbo.Chat_Messages m WHERE m.ChatSessionId = s.ChatSessionId ORDER BY m.SentDate DESC) AS LastMessageText,
        (SELECT TOP 1 m.SentDate FROM dbo.Chat_Messages m WHERE m.ChatSessionId = s.ChatSessionId ORDER BY m.SentDate DESC) AS LastMessageDate,
        r.Rating,
        r.Feedback
    FROM dbo.Chat_Sessions s
    INNER JOIN dbo.Chat_Visitors v ON s.VisitorId = v.VisitorId
    INNER JOIN dbo.Chat_Services srv ON s.ServiceId = srv.ServiceId
    LEFT JOIN dbo.Chat_Agents a ON s.AssignedAgentId = a.AgentId
    LEFT JOIN dbo.Chat_Ratings r ON s.ChatSessionId = r.ChatSessionId
    WHERE (@AgentId IS NULL OR s.AssignedAgentId = @AgentId)
      AND (@ServiceId IS NULL OR s.ServiceId = @ServiceId)
      AND (@SessionStatus IS NULL OR @SessionStatus = '' OR s.SessionStatus = @SessionStatus)
      AND (@SearchMobile IS NULL OR @SearchMobile = '' OR v.MobileNumber LIKE '%' + @SearchMobile + '%')
      AND (@StartDate IS NULL OR s.StartDate >= @StartDate)
      AND (@EndDate IS NULL OR s.StartDate <= @EndDate)
    ORDER BY 
        CASE WHEN s.SessionStatus = 'Pending' THEN 1
             WHEN s.SessionStatus = 'Active' THEN 2
             ELSE 3 END ASC,
        s.StartDate DESC;
END;
GO

-- 20. sp_GetVisitorsList
CREATE OR ALTER PROCEDURE dbo.sp_GetVisitorsList
    @SearchMobile NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        v.VisitorId,
        v.CountryCode,
        v.MobileNumber,
        v.CreatedDate,
        MIN(s.StartDate) AS FirstContactDate,
        MAX(s.StartDate) AS LastContactDate,
        COUNT(s.ChatSessionId) AS TotalChats,
        STRING_AGG(srv.ServiceName, ', ') AS ServicesRequested
    FROM dbo.Chat_Visitors v
    LEFT JOIN dbo.Chat_Sessions s ON v.VisitorId = s.VisitorId
    LEFT JOIN dbo.Chat_Services srv ON s.ServiceId = srv.ServiceId
    WHERE (@SearchMobile IS NULL OR @SearchMobile = '' OR v.MobileNumber LIKE '%' + @SearchMobile + '%')
    GROUP BY v.VisitorId, v.CountryCode, v.MobileNumber, v.CreatedDate
    ORDER BY LastContactDate DESC, v.CreatedDate DESC;
END;
GO

-- 21. sp_GetReports
CREATE OR ALTER PROCEDURE dbo.sp_GetReports
    @ReportType NVARCHAR(30), -- 'Daily', 'Monthly', 'ServiceWise', 'AgentWise', 'Pending', 'Completed'
    @StartDate DATETIME2 = NULL,
    @EndDate DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @ReportType = 'Daily'
    BEGIN
        SELECT 
            CAST(s.StartDate AS DATE) AS ReportDate,
            COUNT(s.ChatSessionId) AS TotalChats,
            SUM(CASE WHEN s.SessionStatus = 'Active' THEN 1 ELSE 0 END) AS ActiveChats,
            SUM(CASE WHEN s.SessionStatus = 'Pending' THEN 1 ELSE 0 END) AS PendingChats,
            SUM(CASE WHEN s.SessionStatus IN ('Completed', 'Closed') THEN 1 ELSE 0 END) AS CompletedChats,
            COUNT(DISTINCT s.VisitorId) AS UniqueVisitors
        FROM dbo.Chat_Sessions s
        WHERE (@StartDate IS NULL OR s.StartDate >= @StartDate)
          AND (@EndDate IS NULL OR s.StartDate <= @EndDate)
        GROUP BY CAST(s.StartDate AS DATE)
        ORDER BY ReportDate DESC;
    END
    ELSE IF @ReportType = 'Monthly'
    BEGIN
        SELECT 
            YEAR(s.StartDate) AS ReportYear,
            MONTH(s.StartDate) AS ReportMonth,
            DATENAME(MONTH, s.StartDate) AS MonthName,
            COUNT(s.ChatSessionId) AS TotalChats,
            SUM(CASE WHEN s.SessionStatus = 'Active' THEN 1 ELSE 0 END) AS ActiveChats,
            SUM(CASE WHEN s.SessionStatus = 'Pending' THEN 1 ELSE 0 END) AS PendingChats,
            SUM(CASE WHEN s.SessionStatus IN ('Completed', 'Closed') THEN 1 ELSE 0 END) AS CompletedChats,
            COUNT(DISTINCT s.VisitorId) AS UniqueVisitors
        FROM dbo.Chat_Sessions s
        WHERE (@StartDate IS NULL OR s.StartDate >= @StartDate)
          AND (@EndDate IS NULL OR s.StartDate <= @EndDate)
        GROUP BY YEAR(s.StartDate), MONTH(s.StartDate), DATENAME(MONTH, s.StartDate)
        ORDER BY ReportYear DESC, ReportMonth DESC;
    END
    ELSE IF @ReportType = 'ServiceWise'
    BEGIN
        SELECT 
            srv.ServiceId,
            srv.ServiceName,
            COUNT(s.ChatSessionId) AS TotalChats,
            SUM(CASE WHEN s.SessionStatus = 'Active' THEN 1 ELSE 0 END) AS ActiveChats,
            SUM(CASE WHEN s.SessionStatus = 'Pending' THEN 1 ELSE 0 END) AS PendingChats,
            SUM(CASE WHEN s.SessionStatus IN ('Completed', 'Closed') THEN 1 ELSE 0 END) AS CompletedChats,
            AVG(CAST(r.Rating AS FLOAT)) AS AvgRating
        FROM dbo.Chat_Services srv
        LEFT JOIN dbo.Chat_Sessions s ON srv.ServiceId = s.ServiceId
             AND (@StartDate IS NULL OR s.StartDate >= @StartDate)
             AND (@EndDate IS NULL OR s.StartDate <= @EndDate)
        LEFT JOIN dbo.Chat_Ratings r ON s.ChatSessionId = r.ChatSessionId
        GROUP BY srv.ServiceId, srv.ServiceName
        ORDER BY TotalChats DESC;
    END
    ELSE IF @ReportType = 'AgentWise'
    BEGIN
        SELECT 
            a.AgentId,
            a.AgentName,
            a.Email,
            a.AvailabilityStatus,
            COUNT(s.ChatSessionId) AS TotalAssignedChats,
            SUM(CASE WHEN s.SessionStatus = 'Active' THEN 1 ELSE 0 END) AS ActiveChats,
            SUM(CASE WHEN s.SessionStatus IN ('Completed', 'Closed') THEN 1 ELSE 0 END) AS CompletedChats,
            AVG(CAST(r.Rating AS FLOAT)) AS AvgRating
        FROM dbo.Chat_Agents a
        LEFT JOIN dbo.Chat_Sessions s ON a.AgentId = s.AssignedAgentId
             AND (@StartDate IS NULL OR s.StartDate >= @StartDate)
             AND (@EndDate IS NULL OR s.StartDate <= @EndDate)
        LEFT JOIN dbo.Chat_Ratings r ON s.ChatSessionId = r.ChatSessionId
        WHERE a.Role = 'Agent'
        GROUP BY a.AgentId, a.AgentName, a.Email, a.AvailabilityStatus
        ORDER BY TotalAssignedChats DESC;
    END
    ELSE IF @ReportType = 'Pending'
    BEGIN
        SELECT 
            s.ChatSessionId,
            v.MobileNumber,
            srv.ServiceName,
            s.StartDate,
            DATEDIFF(MINUTE, s.StartDate, SYSUTCDATETIME()) AS WaitingTimeMinutes
        FROM dbo.Chat_Sessions s
        INNER JOIN dbo.Chat_Visitors v ON s.VisitorId = v.VisitorId
        INNER JOIN dbo.Chat_Services srv ON s.ServiceId = srv.ServiceId
        WHERE s.SessionStatus = 'Pending'
        ORDER BY s.StartDate ASC;
    END
    ELSE IF @ReportType = 'Completed'
    BEGIN
        SELECT 
            s.ChatSessionId,
            v.MobileNumber,
            srv.ServiceName,
            a.AgentName,
            s.StartDate,
            s.EndDate,
            DATEDIFF(MINUTE, s.StartDate, s.EndDate) AS DurationMinutes,
            r.Rating,
            r.Feedback
        FROM dbo.Chat_Sessions s
        INNER JOIN dbo.Chat_Visitors v ON s.VisitorId = v.VisitorId
        INNER JOIN dbo.Chat_Services srv ON s.ServiceId = srv.ServiceId
        LEFT JOIN dbo.Chat_Agents a ON s.AssignedAgentId = a.AgentId
        LEFT JOIN dbo.Chat_Ratings r ON s.ChatSessionId = r.ChatSessionId
        WHERE s.SessionStatus IN ('Completed', 'Closed')
          AND (@StartDate IS NULL OR s.StartDate >= @StartDate)
          AND (@EndDate IS NULL OR s.StartDate <= @EndDate)
        ORDER BY s.EndDate DESC;
    END
END;
GO

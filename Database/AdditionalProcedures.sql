USE TawkToLiveChatDb;
GO

-- 22. sp_GetAllAgents
CREATE OR ALTER PROCEDURE dbo.sp_GetAllAgents
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.AgentId,
        a.AgentName,
        a.Email,
        a.ProfileImage,
        a.AvailabilityStatus,
        a.MaxConcurrentChats,
        a.Role,
        a.IsActive,
        a.CreatedDate,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions cs WHERE cs.AssignedAgentId = a.AgentId AND cs.SessionStatus = 'Active') AS ActiveChatsCount,
        (SELECT COUNT(1) FROM dbo.Chat_Sessions cs WHERE cs.AssignedAgentId = a.AgentId) AS TotalAssignedChats,
        STRING_AGG(srv.ServiceName, ', ') AS AssignedServices
    FROM dbo.Chat_Agents a
    LEFT JOIN dbo.Chat_AgentServices cas ON a.AgentId = cas.AgentId
    LEFT JOIN dbo.Chat_Services srv ON cas.ServiceId = srv.ServiceId
    GROUP BY a.AgentId, a.AgentName, a.Email, a.ProfileImage, a.AvailabilityStatus, a.MaxConcurrentChats, a.Role, a.IsActive, a.CreatedDate
    ORDER BY a.Role ASC, a.AgentName ASC;
END;
GO

-- 23. sp_GetAgentById
CREATE OR ALTER PROCEDURE dbo.sp_GetAgentById
    @AgentId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.AgentId,
        a.AgentName,
        a.Email,
        a.PasswordHash,
        a.ProfileImage,
        a.AvailabilityStatus,
        a.MaxConcurrentChats,
        a.Role,
        a.IsActive,
        a.CreatedDate
    FROM dbo.Chat_Agents a
    WHERE a.AgentId = @AgentId;

    -- Return assigned service IDs
    SELECT ServiceId
    FROM dbo.Chat_AgentServices
    WHERE AgentId = @AgentId;
END;
GO

-- 24. sp_GetAgentByEmail
CREATE OR ALTER PROCEDURE dbo.sp_GetAgentByEmail
    @Email NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.AgentId,
        a.AgentName,
        a.Email,
        a.PasswordHash,
        a.ProfileImage,
        a.AvailabilityStatus,
        a.MaxConcurrentChats,
        a.Role,
        a.IsActive,
        a.CreatedDate
    FROM dbo.Chat_Agents a
    WHERE a.Email = @Email;
END;
GO

-- 25. sp_SaveAgent
CREATE OR ALTER PROCEDURE dbo.sp_SaveAgent
    @AgentId INT,
    @AgentName NVARCHAR(100),
    @Email NVARCHAR(150),
    @PasswordHash NVARCHAR(255) = NULL,
    @ProfileImage NVARCHAR(255) = NULL,
    @MaxConcurrentChats INT = 5,
    @Role NVARCHAR(20) = 'Agent',
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @AgentId <= 0
    BEGIN
        INSERT INTO dbo.Chat_Agents (AgentName, Email, PasswordHash, ProfileImage, AvailabilityStatus, MaxConcurrentChats, Role, IsActive, CreatedDate)
        VALUES (@AgentName, @Email, @PasswordHash, ISNULL(@ProfileImage, '/images/default-avatar.png'), 'Offline', @MaxConcurrentChats, @Role, @IsActive, SYSUTCDATETIME());

        SELECT SCOPE_IDENTITY() AS AgentId;
    END
    ELSE
    BEGIN
        IF @PasswordHash IS NOT NULL AND @PasswordHash <> ''
        BEGIN
            UPDATE dbo.Chat_Agents
            SET AgentName = @AgentName,
                Email = @Email,
                PasswordHash = @PasswordHash,
                ProfileImage = ISNULL(@ProfileImage, ProfileImage),
                MaxConcurrentChats = @MaxConcurrentChats,
                Role = @Role,
                IsActive = @IsActive
            WHERE AgentId = @AgentId;
        END
        ELSE
        BEGIN
            UPDATE dbo.Chat_Agents
            SET AgentName = @AgentName,
                Email = @Email,
                ProfileImage = ISNULL(@ProfileImage, ProfileImage),
                MaxConcurrentChats = @MaxConcurrentChats,
                Role = @Role,
                IsActive = @IsActive
            WHERE AgentId = @AgentId;
        END

        SELECT @AgentId AS AgentId;
    END
END;
GO

-- 26. sp_UpdateAgentAvailability
CREATE OR ALTER PROCEDURE dbo.sp_UpdateAgentAvailability
    @AgentId INT,
    @AvailabilityStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Chat_Agents
    SET AvailabilityStatus = @AvailabilityStatus
    WHERE AgentId = @AgentId;

    SELECT AgentId, AgentName, AvailabilityStatus
    FROM dbo.Chat_Agents
    WHERE AgentId = @AgentId;
END;
GO

-- 27. sp_SetAgentServices
CREATE OR ALTER PROCEDURE dbo.sp_SetAgentServices
    @AgentId INT,
    @ServiceIds NVARCHAR(MAX) -- comma-separated e.g. '1,2,3'
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Chat_AgentServices
    WHERE AgentId = @AgentId;

    IF @ServiceIds IS NOT NULL AND LEN(@ServiceIds) > 0
    BEGIN
        INSERT INTO dbo.Chat_AgentServices (AgentId, ServiceId)
        SELECT @AgentId, CAST(value AS INT)
        FROM STRING_SPLIT(@ServiceIds, ',')
        WHERE RTRIM(LTRIM(value)) <> '';
    END
END;
GO

-- 28. sp_GetAvailableAgentsForTransfer
CREATE OR ALTER PROCEDURE dbo.sp_GetAvailableAgentsForTransfer
    @CurrentAgentId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.AgentId,
        a.AgentName,
        a.Email,
        a.AvailabilityStatus,
        a.MaxConcurrentChats,
        COUNT(cs.ChatSessionId) AS ActiveChats
    FROM dbo.Chat_Agents a
    LEFT JOIN dbo.Chat_Sessions cs ON a.AgentId = cs.AssignedAgentId AND cs.SessionStatus = 'Active'
    WHERE a.AgentId <> @CurrentAgentId
      AND a.IsActive = 1
      AND a.AvailabilityStatus IN ('Online', 'Busy')
    GROUP BY a.AgentId, a.AgentName, a.Email, a.AvailabilityStatus, a.MaxConcurrentChats
    ORDER BY a.AvailabilityStatus ASC, a.AgentName ASC;
END;
GO

-- 29. sp_GetServiceById
CREATE OR ALTER PROCEDURE dbo.sp_GetServiceById
    @ServiceId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        ServiceId,
        ServiceName,
        Description,
        DisplayOrder,
        IsActive
    FROM dbo.Chat_Services
    WHERE ServiceId = @ServiceId;
END;
GO

-- 30. sp_DeleteOrToggleService
CREATE OR ALTER PROCEDURE dbo.sp_DeleteOrToggleService
    @ServiceId INT,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Chat_Services
    SET IsActive = @IsActive
    WHERE ServiceId = @ServiceId;

    SELECT @ServiceId AS ServiceId;
END;
GO

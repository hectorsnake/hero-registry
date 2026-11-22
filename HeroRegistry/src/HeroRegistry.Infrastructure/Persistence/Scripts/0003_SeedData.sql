-- 0003: Demo seed data (T03.1): the administrator account and three heroes.
-- Applied once by DatabaseMigrator. Never edit an applied script: add a new numbered script instead.
--
-- Each row is guarded with NOT EXISTS, so the script never fails or duplicates data on a database
-- that already has these rows (for example, created through the API before this script existed).

SET NOCOUNT ON;

-- Administrator: Admin / Admin (admin@heroregistry.com)
-- The password is stored only as a BCrypt hash (work factor 11, BCrypt.Net-Next format) of the demo
-- password "Admin". This demo password is weaker than the registration policy allows.
-- Change it for any shared or real deployment.
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserName = N'Admin' AND IsDeleted = 0)
BEGIN
    INSERT INTO dbo.Users (UserName, Email, Password, IsAdmin, CreatedBy)
    VALUES (N'Admin',
            N'admin@heroregistry.com',
            N'$2a$11$rnYVgixh.98DnFhT80CktedjRbl0EMfnBjZppegPZdRKmMXDk2uTq',
            1,
            NULL); -- NULL = created by the system
END;

DECLARE @AdminId INT = (SELECT Id FROM dbo.Users WHERE UserName = N'Admin' AND IsDeleted = 0);

-- Heroes, created by Admin. CreatedAt defaults to the current UTC time.
IF NOT EXISTS (SELECT 1 FROM dbo.SuperHeroes WHERE HeroName = N'Captain America' AND IsDeleted = 0)
BEGIN
    INSERT INTO dbo.SuperHeroes
        (HeroName, FirstName, LastName, BirthDate, Email, Superpowers, PowerLevel,
         Address1, Address2, City, State, ZipCode, CreatedBy)
    VALUES
        (N'Captain America', N'Steve', N'Rogers', '1918-07-04', N'captain.america@heroregistry.com',
         N'["Super-soldier strength","Enhanced agility","Shield combat","Tactical leadership"]', 85,
         N'569 Leaman Place', NULL, N'Brooklyn', 'NY', '11201', @AdminId);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SuperHeroes WHERE HeroName = N'Spider-Man' AND IsDeleted = 0)
BEGIN
    INSERT INTO dbo.SuperHeroes
        (HeroName, FirstName, LastName, BirthDate, Email, Superpowers, PowerLevel,
         Address1, Address2, City, State, ZipCode, CreatedBy)
    VALUES
        (N'Spider-Man', N'Peter', N'Parker', '2001-08-10', N'spider-man@heroregistry.com',
         N'["Wall-crawling","Spider-sense","Superhuman strength","Web-slinging"]', 80,
         N'20 Ingram Street', NULL, N'Queens', 'NY', '11375', @AdminId);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SuperHeroes WHERE HeroName = N'Superman' AND IsDeleted = 0)
BEGIN
    INSERT INTO dbo.SuperHeroes
        (HeroName, FirstName, LastName, BirthDate, Email, Superpowers, PowerLevel,
         Address1, Address2, City, State, ZipCode, CreatedBy)
    VALUES
        (N'Superman', N'Clark', N'Kent', '1977-06-18', N'superman@heroregistry.com',
         N'["Flight","Super strength","Heat vision","Invulnerability","X-ray vision"]', 100,
         N'344 Clinton Street', N'Apt 3D', N'Metropolis', 'NY', '10001', @AdminId);
END;
GO

-- Insert a new contact into the Contacts table (Azure SQL Database)

INSERT INTO Contacts (
    Id,
    FirstName,
    LastName,
    Email,
    Phone,
    Company,
    Service,
    Budget,
    Message,
    TermsOfService,
    CreatedDate,
    LastModified
) VALUES (
    NEWID(),
    'John',
    'Doe',
    'john.doe@example.com',
    '+1-555-0123',
    'Example Corp',
    'Web Development',
    '$10,000 - $25,000',
    'We need a new website with modern design and e-commerce functionality.',
    1,
    GETUTCDATE(),
    GETUTCDATE()
);

-- Or with a specific GUID:
-- INSERT INTO Contacts (
--     Id,
--     FirstName,
--     LastName,
--     Email,
--     Phone,
--     Company,
--     Service,
--     Budget,
--     Message,
--     TermsOfService,
--     CreatedDate,
--     LastModified
-- ) VALUES (
--     '12345678-1234-1234-1234-123456789012',
--     'Jane',
--     'Smith',
--     'jane.smith@example.com',
--     '+1-555-0124',
--     'Tech Solutions Inc',
--     'Mobile App Development',
--     '$25,000 - $50,000',
--     'Looking for iOS and Android app development services.',
--     1,
--     GETUTCDATE(),
--     GETUTCDATE()
-- );

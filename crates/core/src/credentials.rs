use keyring::Entry;
use crate::error::{Error, Result};

const SERVICE_NAME: &str = "kmitlnetauth";

pub struct CredentialManager;

impl CredentialManager {
    pub fn set_password(username: &str, password: &str) -> Result<()> {
        let entry = Entry::new(SERVICE_NAME, username)
            .map_err(|e| Error::Config(format!("Failed to create keyring entry: {}", e)))?;
        
        entry.set_password(password)
            .map_err(|e| Error::Config(format!("Failed to save password to keyring: {}", e)))?;
        
        Ok(())
    }

    pub fn get_password(username: &str) -> Result<String> {
        let entry = Entry::new(SERVICE_NAME, username)
            .map_err(|e| Error::Config(format!("Failed to create keyring entry: {}", e)))?;
        
        match entry.get_password() {
            Ok(pwd) => Ok(pwd),
            Err(keyring::Error::NoEntry) => {
                // If not found in keyring, return empty or handle gracefully
                // Depending on requirement, we might just return empty string to indicate "not found"
                // but strictly it's an error if we expected it. 
                // Let's return error to let caller decide.
                Err(Error::Config("Password not found in keyring".to_string()))
            },
            Err(e) => Err(Error::Config(format!("Failed to retrieve password from keyring: {}", e))),
        }
    }

    pub fn delete_password(username: &str) -> Result<()> {
         let entry = Entry::new(SERVICE_NAME, username)
            .map_err(|e| Error::Config(format!("Failed to create keyring entry: {}", e)))?;
            
        entry.delete_credential()
            .map_err(|e| Error::Config(format!("Failed to delete password from keyring: {}", e)))?;
        Ok(())
    }
}

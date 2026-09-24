use log::debug;
use std::collections::HashMap;
use std::fs;
use std::path::Path;
use uuid::Uuid;

#[derive(Debug, Clone)]
pub struct PackageReference {
    pub provider: String,
    pub configuration: HashMap<String, String>,
}

pub struct PackageController {
    package_reference: PackageReference,
}

impl PackageController {
    pub const BUILTIN_ID: Uuid = Uuid::from_u128(0x97d297d2df6f4c859d0786bc64a041a6);
    pub const BUILTIN_PROVIDER: &'static str = "builtin";

    pub async fn get_versions(&self) -> Result<Vec<String>, String> {
        let provider = &self.package_reference.provider;

        debug!("Get package versions using provider {provider}");

        match provider.as_str() {
            Self::BUILTIN_PROVIDER => Ok(vec!["current".to_string()]),
            "local" => self.get_local_versions().await,
            "git-tag" => self.get_git_tags().await,
            _ => Err(format!("The provider {provider} is not supported.")),
        }
    }

    pub async fn load(&self, restore_root: &str) -> Result<(), String> {
        let entrypoint = self
            .package_reference
            .configuration
            .get("entrypoint")
            .ok_or("The 'entrypoint' parameter is required in the package reference.")?;

        let import_path = self
            .package_reference
            .configuration
            .get("import")
            .ok_or("The 'import' parameter is required in the package reference.")?;

        if self.package_reference.provider == Self::BUILTIN_PROVIDER {
            // not implemented (there is no need for it right now)
            return Err("Loading built-in extensions is not supported.".to_string());
        }

        let restore_folder_path = self.restore(restore_root).await?;
        let venv_folder_path = Path::new(&restore_folder_path).join(".venv");

        // Simulate loading the module (Rust doesn't have dynamic imports like Python)
        debug!("Loaded module {} from {}", import_path, restore_folder_path);

        Ok(())
    }

    async fn restore(&self, restore_root: &str) -> Result<String, String> {
        let actual_restore_root = Path::new(restore_root).join(&self.package_reference.provider);

        debug!(
            "Restore package to {:?} using provider {}",
            actual_restore_root, self.package_reference.provider
        );

        match self.package_reference.provider.as_str() {
            "local" => self.restore_local(&actual_restore_root).await,
            "git-tag" => self.restore_git_tag(&actual_restore_root).await,
            _ => Err(format!(
                "The provider {} is not supported.",
                self.package_reference.provider
            )),
        }
    }

    async fn restore_local(&self, restore_root: &Path) -> Result<String, String> {
        let configuration = &self.package_reference.configuration;
        let path = configuration
            .get("path")
            .ok_or("The 'path' parameter is required in the package reference.")?;
        let version = configuration
            .get("version")
            .ok_or("The 'version' parameter is required in the package reference.")?;

        let source_folder_path = Path::new(path).join(version);
        if !source_folder_path.exists() {
            return Err(format!(
                "The source path {:?} does not exist.",
                source_folder_path
            ));
        }

        let path_hash = Self::hash_string(path);
        let target_folder_path = restore_root.join(path_hash).join(version);

        if !target_folder_path.exists() {
            fs::create_dir_all(&target_folder_path).map_err(|e| e.to_string())?;
            self.clone_folder(&source_folder_path, &target_folder_path)?;
        } else {
            debug!("Package is already restored");
        }

        Ok(target_folder_path.to_string_lossy().to_string())
    }

    async fn restore_git_tag(&self, restore_root: &Path) -> Result<String, String> {
        let configuration = &self.package_reference.configuration;
        let repository = configuration
            .get("repository")
            .ok_or("The 'repository' parameter is required in the package reference.")?;
        let tag = configuration
            .get("tag")
            .ok_or("The 'tag' parameter is required in the package reference.")?;

        let escaped_uri = Self::escape_url(repository);
        let target_folder_path = restore_root.join(escaped_uri).join(tag);

        if !target_folder_path.exists() {
            let clone_folder_path = tempfile::tempdir().map_err(|e| e.to_string())?;
            let clone_folder_path_str = clone_folder_path.path().to_string_lossy();

            let output = SmolCommand::new("git")
                .arg("clone")
                .arg("--depth")
                .arg("1")
                .arg("--branch")
                .arg(tag)
                .arg(repository)
                .arg(&clone_folder_path_str)
                .output()
                .await
                .map_err(|e| e.to_string())?;

            if !output.status.success() {
                return Err(format!(
                    "Unable to clone repository {}. Reason: {}",
                    repository,
                    String::from_utf8_lossy(&output.stderr)
                ));
            }

            self.clone_folder(clone_folder_path.path(), &target_folder_path)?;
        } else {
            debug!("Package is already restored");
        }

        Ok(target_folder_path.to_string_lossy().to_string())
    }

    async fn get_local_versions(&self) -> Result<Vec<String>, String> {
        let configuration = &self.package_reference.configuration;
        let path = configuration
            .get("path")
            .ok_or("The 'path' parameter is missing in the package reference.")?;

        if !Path::new(path).exists() {
            return Err(format!("The extension path {} does not exist.", path));
        }

        let mut versions = vec![];
        for entry in fs::read_dir(path).map_err(|e| e.to_string())? {
            let entry = entry.map_err(|e| e.to_string())?;
            if entry.file_type().map_err(|e| e.to_string())?.is_dir() {
                if let Some(folder_name) = entry.file_name().to_str() {
                    versions.push(folder_name.to_string());
                }
            }
        }

        versions.sort_by(|a, b| b.cmp(a));
        Ok(versions)
    }

    async fn get_git_tags(&self) -> Result<Vec<String>, String> {
        let configuration = &self.package_reference.configuration;
        let repository = configuration
            .get("repository")
            .ok_or("The 'repository' parameter is missing in the package reference.")?;

        let output = SmolCommand::new("git")
            .arg("ls-remote")
            .arg("--tags")
            .arg("--sort=v:refname")
            .arg("--refs")
            .arg(repository)
            .output()
            .await
            .map_err(|e| e.to_string())?;

        if !output.status.success() {
            return Err(format!(
                "Unable to find tags for repository {}. Reason: {}",
                repository,
                String::from_utf8_lossy(&output.stderr)
            ));
        }

        let tags = String::from_utf8_lossy(&output.stdout)
            .lines()
            .filter_map(|line| {
                let parts: Vec<&str> = line.split('\t').collect();
                if parts.len() > 1 && parts[1].starts_with("refs/tags/") {
                    Some(parts[1].replace("refs/tags/", ""))
                } else {
                    None
                }
            })
            .collect();

        Ok(tags)
    }

    fn clone_folder(&self, source: &Path, target: &Path) -> Result<(), String> {
        if !source.exists() {
            return Err("The source directory does not exist.".to_string());
        }

        fs::create_dir_all(target).map_err(|e| e.to_string())?;
        fs_extra::dir::copy(source, target, &fs_extra::dir::CopyOptions::new())
            .map_err(|e| e.to_string())?;
        Ok(())
    }

    fn escape_url(url: &str) -> String {
        url.replace("://", "_").replace('/', "_")
    }

    fn hash_string(value: &str) -> String {
        let digest = md5::compute(value);

        format!("{:x}", digest)
    }
}

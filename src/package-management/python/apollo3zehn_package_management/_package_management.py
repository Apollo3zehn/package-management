import asyncio
import hashlib
import importlib.util
import os
import shutil
import subprocess
import sys
import tempfile
from logging import Logger
from types import ModuleType
from typing import List, Optional, cast
from urllib.parse import urlparse, urlunparse
from uuid import UUID
from venv import EnvBuilder

from ._typedefs import PackageReference


class PackageController:

    BUILTIN_ID = UUID("97d297d2-df6f-4c85-9d07-86bc64a041a6")
    BUILTIN_PROVIDER = "builtin"

    _module: Optional[ModuleType] = None

    def __init__(self, package_reference: PackageReference, logger: Logger):
        self._package_reference = package_reference
        self._logger = logger

    async def get_versions(self) -> List[str]:

        self._logger.debug(f"Get package versions using provider {self._package_reference.provider}")

        if self._package_reference.provider == self.BUILTIN_PROVIDER:
            return ["current"]
        
        elif self._package_reference.provider == "local":
            return await self._get_local_versions()
        
        elif self._package_reference.provider == "git-tag":
            return await self._get_git_tags()
        
        else:
            raise ValueError(f"The provider {self._package_reference.provider} is not supported.")

    async def load(self, restore_root: str) -> ModuleType:

        entrypoint = self._package_reference.configuration.get("entrypoint")
        import_path = self._package_reference.configuration.get("import")

        if not entrypoint or not import_path:
            raise ValueError("The 'entrypoint' and 'import' parameters are required in the package reference.")

        if self._module is not None:
            raise Exception("The extension is already loaded.")

        if self._package_reference.provider == self.BUILTIN_PROVIDER:

            # not implemented (there is no need for it right now)
            raise NotImplementedError("Loading built-in extensions is not supported.")

        else:

            restore_folder_path = await self._restore(restore_root)
            original_sys_path = sys.path.copy() # Temporarily modify sys.path to include the extension's environment

            try:

                minor_version = sys.version_info.minor
                venv_folder_path = os.path.join(restore_folder_path, ".venv")
                entrypoint_folder_path = os.path.join(restore_folder_path, entrypoint)

                sys.path.insert(0, entrypoint_folder_path)
                sys.path.insert(0, os.path.join(venv_folder_path, "lib", f"python3.{minor_version}", "site-packages"))

                self._module = importlib.import_module(import_path)
                
                return self._module

            finally:

                sys.path = original_sys_path

    def unload(self):

        if self._module is None:
            raise Exception("The extension is not yet loaded.")
        
        self._module = None

    async def _restore(self, restore_root: str) -> str:

        actual_restore_root = os.path.join(restore_root, self._package_reference.provider)

        self._logger.debug(f"Restore package to {actual_restore_root} using provider {self._package_reference.provider}")

        restore_folder_path: str

        if self._package_reference.provider == "local":
            restore_folder_path = await self._restore_local(actual_restore_root)
        
        elif self._package_reference.provider == "git-tag":
            restore_folder_path = await self._restore_git_tag(actual_restore_root)
        
        else:
            raise ValueError(f"The provider {self._package_reference.provider} is not supported.")
        
        requirements_file_path = os.path.join(restore_folder_path, "requirements.txt")
        venv_folder_path = os.path.join(restore_folder_path, ".venv")

        if os.path.exists(requirements_file_path) and not os.path.exists(venv_folder_path):
            PackageController._create_virtual_environment(venv_folder_path, requirements_file_path)

        return restore_folder_path

    def _clone_folder(self, source: str, target: str):

        if not os.path.exists(source):
            raise FileNotFoundError("The source directory does not exist.")

        os.makedirs(target, exist_ok=True)
        shutil.copytree(source, target, dirs_exist_ok=True)

    @staticmethod
    def _create_virtual_environment(venv_folder_path: str, requirements_file_path: str):

        # Create virtual environment
        builder = EnvBuilder(with_pip=True)
        builder.create(venv_folder_path)

        # Install dependencies
        if os.path.exists(requirements_file_path):
            pip_executable_path = os.path.join(venv_folder_path, "bin", "pip")
            subprocess.check_call([pip_executable_path, "install", "-r", requirements_file_path])

    #region local

    async def _get_local_versions(self) -> List[str]:

        raw_result = []
        configuration = self._package_reference.configuration
        path = configuration.get("path")

        if not path:
            raise ValueError("The 'path' parameter is missing in the package reference.")

        if not os.path.exists(path):
            raise FileNotFoundError(f"The extension path {path} does not exist.")

        for folder_path in os.listdir(path):

            folder_name = os.path.basename(folder_path)

            raw_result.append(folder_name)
            self._logger.debug(f"Found package version {folder_name}")

        return sorted(raw_result, reverse=True)

    async def _restore_local(self, restore_root: str) -> str:

        configuration = self._package_reference.configuration

        path = configuration.get("path")
        version = configuration.get("version")

        if not path or not version:
            raise ValueError("The 'path' and 'version' parameters are required in the package reference.")

        source_folder_path = os.path.join(path, version)

        if not os.path.exists(source_folder_path):
            raise FileNotFoundError(f"The source path {source_folder_path} does not exist.")

        path_hash = PackageController._hash_string(path)
        target_folder_path = os.path.join(restore_root, path_hash, version)

        if not os.path.exists(target_folder_path) or not os.listdir(target_folder_path):
            self._clone_folder(source_folder_path, target_folder_path)

        else:
            self._logger.debug("Package is already restored")

        return target_folder_path

    #region git-tag
    
    async def _get_git_tags(self) -> List[str]:

        REFS_PREFIX = "refs/tags/"

        result = []
        configuration = self._package_reference.configuration

        repository = configuration.get("repository")

        if not repository:
            raise ValueError("The 'repository' parameter is missing in the package reference.")

        process = await asyncio.create_subprocess_exec(
            "git", "ls-remote", "--tags", "--sort=v:refname", "--refs", repository,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE
        )

        stdout, stderr = await process.communicate()

        if process.returncode != 0:
            raise Exception(f"Unable to find tags for repository {repository}. Reason: {stderr.decode()}")

        for ref_line in stdout.decode().splitlines():

            ref_string = ref_line.split('\t')[1]

            if ref_string.startswith(REFS_PREFIX):
                tag = ref_string[len(REFS_PREFIX):]
                result.append(tag)

            else:
                self._logger.debug(f"Unable to extract tag from ref {ref_line}")

        return result[::-1]

    async def _restore_git_tag(self, restore_root: str) -> str:
        
        configuration = self._package_reference.configuration

        repository = configuration.get("repository")
        tag = configuration.get("tag")

        if not repository or not tag:
            raise ValueError("The 'repository' and 'tag' parameters are required in the package reference.")

        escaped_uri_1 = PackageController._escape_url(repository).replace("://", "_").replace("/", "_")
        target_folder_path = os.path.join(restore_root, escaped_uri_1, tag)

        if not os.path.exists(target_folder_path) or not os.listdir(target_folder_path):

            clone_folder_path = os.path.join(tempfile.gettempdir(), os.urandom(24).hex())
            escaped_uri_2 = PackageController._escape_url(repository)

            try:

                process = await asyncio.create_subprocess_exec(
                    "git", "clone", "--depth", "1", "--branch", tag, "--recurse-submodules", repository, clone_folder_path,
                    stdout=subprocess.PIPE, stderr=subprocess.PIPE
                )

                _, stderr = await process.communicate()

                if process.returncode != 0:
                    raise Exception(f"Unable to clone repository {escaped_uri_2}. Reason: {stderr.decode()}")

                self._clone_folder(clone_folder_path, target_folder_path)

            except:
                
                # Try to delete restore folder
                try:
                    if os.path.exists(target_folder_path):
                        shutil.rmtree(target_folder_path)

                except:
                    pass

                raise

            finally:

                # Try to delete clone folder
                try:
                    if os.path.exists(clone_folder_path):
                        shutil.rmtree(clone_folder_path)
                        
                except:
                    pass


        else:
            self._logger.debug("Package is already restored")

        return target_folder_path
   
    @staticmethod
    def _escape_url(url: str):

        parsed_url = urlparse(url)
        netloc = cast(str, parsed_url.hostname)

        if parsed_url.port is not None:
            netloc += f":{parsed_url.port}"

        cleaned_url = parsed_url._replace(netloc=netloc)

        return urlunparse(cleaned_url)
    
    @staticmethod
    def _hash_string(value: str) -> str:

        md5 = hashlib.md5()
        md5.update(value.encode('utf-8'))
        hashed_string = md5.hexdigest()
        
        return hashed_string
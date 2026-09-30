import inspect
import os
import shutil
import tempfile
import uuid
from logging import Logger
from unittest.mock import Mock

import pytest
from apollo3zehn_package_management import PackageReference
from apollo3zehn_package_management._package_management import PackageController

from _test_extension_repository import get_test_extension_repository


#region Load
@pytest.mark.asyncio
async def can_load_and_unload_test():

    # Arrange
    restore_root = os.path.join(tempfile.gettempdir(), f"PackageManagement.Tests.{uuid.uuid4()}")
    os.makedirs(restore_root)

    try:

        package_reference = PackageReference(
            provider="git-tag",
            configuration={
                "repository": get_test_extension_repository(),
                "tag": "v0.1.0",
                "entrypoint": "src",
                "import": "my_package.my_module"
            }
        )

        # Act
        await _load_run_and_unload(restore_root, package_reference)

    finally:

        shutil.rmtree(restore_root, ignore_errors=True)

async def _load_run_and_unload(restore_root, package_reference):
    
    # load
    logger = Mock()
    package_controller = PackageController(package_reference, logger)
    module = await package_controller.load(restore_root)

    logger_type = next(
        type[1] for type in inspect.getmembers(module) \
            if type[1] is not Logger and issubclass(type[1], Logger)
    )
  
    # run
    logger = logger_type() # pyright: ignore

    # unload
    package_controller.unload()

#region Provider: git_tag local

@pytest.mark.asyncio
async def can_get_versions_git_tag_local_test():

    # Arrange
    expected = [
        "v2.0.0",
        "v1.1.1",
        "v1.0.1",
        "v1.0.0-beta2+12347",
        "v1.0.0-beta1+12346",
        "v1.0.0-alpha1+12345",
        "v0.1.0"
    ]

    package_reference = PackageReference(
        provider="git-tag",
        configuration={
            "repository": get_test_extension_repository(),
        }
    )

    package_controller = PackageController(package_reference, Mock())

    # Act
    actual = await package_controller.get_versions()

    # Assert
    assert expected == actual

@pytest.mark.asyncio
async def can_restore_git_tag_local_test():
    
    # Arrange
    restore_root = os.path.join(tempfile.gettempdir(), f"PackageManagement.Tests.{uuid.uuid4()}")
    os.makedirs(restore_root)

    try:

        package_reference = PackageReference(
            provider="git-tag",
            configuration={
                "repository": get_test_extension_repository(),
                "tag": "v0.1.0",
                "entrypoint": "src"
            }
        )

        package_controller = PackageController(package_reference, Mock())

        # Act        
        restore_folder_path = await package_controller._restore(restore_root)

        # Assert
        assert os.path.exists(os.path.join(restore_folder_path, "src", "my_package", "my_module.py"))

    finally:
        shutil.rmtree(restore_root, ignore_errors=True)

#region Provider: git_tag

@pytest.mark.asyncio
async def can_get_versions_git_tag_test():

    # Arrange

    expected = [
        "v2.0.0-beta.1",
        "v2.0.0",
        "v1.1.1",
        "v1.0.1",
        "v1.0.0-beta2+12347",
        "v1.0.0-beta1+12346",
        "v1.0.0-alpha1+12345",
        "v0.1.0"
    ]

    package_reference = PackageReference(
        provider="git-tag",
        configuration={
            "repository": "https://github.com/Apollo3zehn/git-tags-provider-test-project"
        }
    )

    package_controller = PackageController(package_reference, Mock())

    # Act
    actual = await package_controller.get_versions()

    # Assert
    assert expected == actual

@pytest.mark.asyncio
async def can_restore_git_tag_test():

    # Arrange
    version = "v2.0.0-beta.1"
    restore_root = os.path.join(tempfile.gettempdir(), f"PackageManagement.Tests.{uuid.uuid4()}")
    restore_folder_path = os.path.join(restore_root, "git-tag", "https_github.com_Apollo3zehn_git-tags-provider-test-project", version)

    os.makedirs(restore_root)

    try:

        package_reference = PackageReference(
            provider="git-tag",
            configuration={
                "repository": "https://github.com/Apollo3zehn/git-tags-provider-test-project",
                "tag": version,
                "entrypoint": ""
            }
        )

        package_controller = PackageController(package_reference, Mock())

        # Act
        await package_controller._restore(restore_root)

        # Assert
        assert os.path.exists(os.path.join(restore_folder_path, "my_logger.py"))

    finally:
        shutil.rmtree(restore_root, ignore_errors=True)

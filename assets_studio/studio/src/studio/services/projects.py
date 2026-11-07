"""Projects: parts of the library that assets and jobs belong to (ADR-010)."""

import re

from model_server_sdk.contract import Slug
from pydantic import TypeAdapter, ValidationError

from studio.domain import DEFAULT_PROJECT, Project
from studio.storage import repository
from studio.storage.repository import Repository, now

__all__ = ["DEFAULT_PROJECT", "ProjectConflict", "ProjectError", "Projects"]

_SLUG: TypeAdapter[str] = TypeAdapter(Slug)
_MAX_TITLE = 100
# Russian letters for ids made from titles: "Выучка" becomes "vyuchka".
_CYRILLIC = dict(
    zip(
        "абвгдеёжзийклмнопрстуфхцчшщъыьэюя",
        (
            *("a", "b", "v", "g", "d", "e", "e", "zh", "z", "i", "y", "k", "l", "m", "n", "o"),
            *("p", "r", "s", "t", "u", "f", "kh", "ts", "ch", "sh", "shch", "", "y", "", "e"),
            *("yu", "ya"),
        ),
        strict=True,
    )
)


class ProjectError(ValueError):
    """No such project, or a bad id or title."""


class ProjectConflict(ValueError):
    """The id is taken, or the project cannot be deleted while it has assets or unfinished jobs."""


def slug(title: str) -> str:
    """An id from a title: lowercase Latin letters and digits joined by hyphens."""
    letters = "".join(_CYRILLIC.get(char, char) for char in title.lower())
    return re.sub(r"[^a-z0-9]+", "-", letters).strip("-")[:64].strip("-")


def _title(title: str) -> str:
    clean = " ".join(title.split())
    if not clean or len(clean) > _MAX_TITLE:
        raise ProjectError(f"a project title has 1-{_MAX_TITLE} characters")
    return clean


class Projects:
    def __init__(self, repository: Repository) -> None:
        self._repository = repository

    def all(self) -> list[Project]:
        """Projects not deleted, the default one first, with their asset counts."""
        return self._repository.projects()

    def find(self, project_id: str) -> Project | None:
        """A project, deleted ones included (their finished jobs keep referring to them)."""
        return self._repository.project(project_id)

    def get(self, project_id: str) -> Project:
        """``ProjectError`` if there is none or it was deleted."""
        project = self._repository.project(project_id)
        if project is None:
            raise ProjectError(f"project {project_id} not found")
        if project.deleted_at is not None:
            raise ProjectError(f"project {project_id} was deleted")
        return project

    def create(self, title: str, project_id: str | None = None) -> Project:
        """A new project. Without an id, one is made from the title (with a number when taken)."""
        title = _title(title)
        if project_id is not None:
            try:
                _SLUG.validate_python(project_id)
            except ValidationError:
                raise ProjectError(
                    f"bad project id {project_id!r}: lowercase letters and digits, joined by - or _"
                ) from None
            self._add(project_id, title)
        else:
            base = slug(title) or "project"
            for number in range(1, 1000):
                suffix = "" if number == 1 else f"-{number}"
                try:
                    project_id = base[: 64 - len(suffix)].strip("-") + suffix
                    self._add(project_id, title)
                    break
                except ProjectConflict:
                    continue
            else:
                raise ProjectConflict(f"every id like {base} is taken")
        return self.get(project_id)

    def rename(self, project_id: str, title: str) -> Project:
        try:
            self._repository.rename_project(project_id, _title(title))
        except repository.ProjectNotFound as error:
            raise ProjectError(str(error)) from error
        return self.get(project_id)

    def delete(self, project_id: str) -> Project:
        """Delete an empty project: nothing in the library, no unfinished jobs. Its finished
        jobs stay in the history of every project (GET /api/jobs without a project)."""
        try:
            self._repository.delete_project(project_id)
        except repository.ProjectNotFound as error:
            raise ProjectError(str(error)) from error
        except repository.ProjectNotEmpty as error:
            raise ProjectConflict(str(error)) from error
        deleted = self._repository.project(project_id)
        assert deleted is not None
        return deleted

    def _add(self, project_id: str, title: str) -> None:
        try:
            self._repository.add_project(Project(id=project_id, title=title, created_at=now()))
        except repository.ProjectExists as error:
            raise ProjectConflict(str(error)) from error

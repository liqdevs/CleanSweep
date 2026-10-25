# CleanSweep

CleanSweep helps you find and remove unnecessary files on Windows. It is free to use and has no ads or subscription.

## What you can do

- Find temporary files, caches, and other cleanup candidates.
- Review items before deleting them.
- Browse disk usage, search for large files, and choose files or folders to remove.
- Find identical files by content and review extra copies before removing them.
- Find empty folders and review them before removal.
- Manage which apps start with Windows.

## Use it safely

CleanSweep's file-importance labels are estimates based on file names and locations; the app does not inspect file contents. An unknown or low-importance label is not a guarantee that a file is safe to delete. Review the list before confirming removal.

## Command-line cleanup

For automated cleanup, run `CleanSweep.exe --silent --drive C:\ --categories temp,browser_cache`. Supply a drive root and one or more comma-separated category keys. This permanently deletes files without opening a review window, so use only category keys you have verified. The process exit code is `0` for success, `2` for invalid arguments, `3` for scan failure, `4` when cleanup skipped or failed items, and `5` for an unexpected error.

## License

CleanSweep uses the MIT License. In plain terms, you may use, copy, modify, and share it, including commercially, as long as copies include the copyright notice and license. The app comes without a warranty. See [LICENSE](LICENSE) for the full terms.

#!/usr/bin/env python3
"""Build signed apt/rpm metadata locally; no upload or key generation is performed."""
import argparse
import pathlib
import shutil
import subprocess


def run(command, directory, destination=None):
    if destination is None:
        subprocess.run(command, cwd=directory, check=True)
    else:
        with destination.open('wb') as output:
            subprocess.run(command, cwd=directory, stdout=output, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    parser.add_argument('--key-id', required=True, help='Existing GPG signing key fingerprint')
    args = parser.parse_args()
    source = args.packages.resolve()
    target = args.output.resolve()
    packages = list(source.glob('*.deb')) + list(source.glob('*.rpm'))
    if not packages:
        parser.error('No deb/rpm packages found')
    for kind in ['apt', 'rpm']:
        repository = target / kind
        repository.mkdir(parents=True, exist_ok=True)
        extension = '.deb' if kind == 'apt' else '.rpm'
        matching = [package for package in packages if package.suffix == extension]
        if not matching:
            continue
        for package in matching:
            shutil.copy2(package, repository / package.name)
        if kind == 'apt':
            run(['apt-ftparchive', 'packages', '.'], repository, repository / 'Packages')
            run(['apt-ftparchive', 'release', '.'], repository, repository / 'Release')
            run(['gpg', '--batch', '--yes', '--local-user', args.key_id, '--clearsign', '--output', 'InRelease', 'Release'], repository)
            run(['gpg', '--batch', '--yes', '--local-user', args.key_id, '--armor', '--detach-sign', '--output', 'Release.gpg', 'Release'], repository)
        else:
            run(['createrepo_c', '.'], repository)
            run(['gpg', '--batch', '--yes', '--local-user', args.key_id, '--armor', '--detach-sign', '--output', 'repodata/repomd.xml.asc', 'repodata/repomd.xml'], repository)
        run(['gpg', '--armor', '--export', args.key_id], repository, repository / 'sharpai-tui-key.asc')


if __name__ == '__main__':
    main()

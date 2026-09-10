#!/usr/bin/env python3
"""Guard against a green CI run that never touched PostgreSQL.

`dotnet test` exits 0 whether the integration tests ran or were skipped, so
the exit code alone cannot tell those apart. This reads the two TRX files
the workflow produces and fails when the database-backed run did not
actually execute the integration tests.

Usage: verify_test_results.py <unit.trx> <integration.trx>
"""

import sys
import xml.etree.ElementTree as ElementTree

TRX_NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def read_counters(path):
    """Return (total, executed, passed, failed, skipped) from a TRX file."""
    counters = ElementTree.parse(path).getroot().find(".//t:Counters", TRX_NAMESPACE)
    if counters is None:
        raise SystemExit(f"{path}: no <Counters> element - is this a TRX file?")

    total = int(counters.get("total", 0))
    executed = int(counters.get("executed", 0))
    passed = int(counters.get("passed", 0))
    failed = int(counters.get("failed", 0))
    # TRX records a skipped test as "not executed".
    skipped = total - executed

    return total, executed, passed, failed, skipped


def main(argv):
    if len(argv) != 3:
        raise SystemExit("usage: verify_test_results.py <unit.trx> <integration.trx>")

    unit_path, integration_path = argv[1], argv[2]

    u_total, _, u_passed, u_failed, u_skipped = read_counters(unit_path)
    i_total, i_executed, i_passed, i_failed, i_skipped = read_counters(integration_path)

    print("Run 1 - no database configured")
    print(f"  total {u_total}  passed {u_passed}  failed {u_failed}  skipped {u_skipped}")
    print("Run 2 - full suite against PostgreSQL")
    print(f"  total {i_total}  passed {i_passed}  failed {i_failed}  skipped {i_skipped}")

    problems = []

    if u_failed or i_failed:
        problems.append("a test failed")

    # The whole point of run 2. Anything skipped there means the fixture
    # never got a usable database and the integration suite silently
    # did not run.
    if i_skipped != 0:
        problems.append(
            f"run 2 skipped {i_skipped} test(s) - the PostgreSQL integration suite did not execute"
        )

    # Run 1 skips the integration tests by design; run 2 must therefore
    # execute strictly more than run 1 did.
    if u_skipped == 0:
        problems.append(
            "run 1 skipped nothing - the PostgreSQL tests are no longer gated by "
            "[PostgresFact], so run 1 may be reaching a database it should not have"
        )

    if i_executed <= u_passed:
        problems.append(
            f"run 2 executed {i_executed} test(s), no more than run 1's {u_passed} - "
            "the integration tests did not add anything"
        )

    if i_total != u_total:
        problems.append(
            f"the two runs discovered different test counts ({u_total} vs {i_total})"
        )

    if problems:
        for problem in problems:
            print(f"::error::{problem}")
        return 1

    print(
        f"OK: {i_passed} tests passed with 0 skipped, "
        f"including the {u_skipped} PostgreSQL integration test(s) that run 1 skipped."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

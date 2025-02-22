#include <algorithm>
#include <cstddef>
#include <iostream>
#include <vector>

using std::sort;
using std::vector;

namespace {

/*
 * This is a canonical 3SUM problem—a model problem to which many
 * others, especially geometric ones, are often reduced.  The
 * algorithm implemented here runs in O(n^2) time, which is considered
 * optimal.
 */

bool three_sum(vector<int> &a, int sum) {
  // EDGE_CASE: input vector has less than 3 elements.
  if (a.size() < 3) {
    return false;
  }

  // a has to be sorted.
  for (size_t f = 0; f < a.size() - 2; ++f) {
    size_t l = f + 1;
    size_t r = a.size() - 1;

    while (l < r) {
      auto local = a[f] + a[l] + a[r];

      if (local < sum) {
        l++;
      } else if (local > sum) {
        r--;
      } else {
        return true;
      }
    }
  }

  return false;
}

}  // namespace

int main(int /*argc*/, char * /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;

  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  sort(a.begin(), a.end());

  int sum = 0;
  std::cin >> sum;

  std::cout << (three_sum(a, sum) ? "YES" : "NO") << "\n";

  return 0;
}

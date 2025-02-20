#include <algorithm>
#include <cstddef>
#include <iostream>
#include <vector>

using std::sort;
using std::vector;

namespace {

// Vector A has to be sorted.
bool three_sum(vector<int> &a, int sum) {
  if (a.size() < 3) {
    return false;
  }

  // O(n^2)
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

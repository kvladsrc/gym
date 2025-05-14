#include <cstddef>
#include <iostream>
#include <optional>
#include <string>
#include <vector>

using std::optional;
using std::string;
using std::vector;

namespace {

optional<size_t> lower_bound(vector<int> &a, int i) {
  // EDGE_CASE: empty vector do not contain any elements.
  if (a.empty()) {
    return std::nullopt;
  }

  int l = 0;
  int r = a.size() - 1;
  std::optional<size_t> res = std::nullopt;

  while (l <= r) {
    auto const mid = (l + r) / 2;
    if (a[mid] >= i) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

}  // namespace

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int el = 0;
  std::cin >> el;
  auto ub = lower_bound(a, el);
  if (ub) {
    std::cout << *ub << "\n";
  } else {
    std::cout << -1 << "\n";
  }

  return 0;
}

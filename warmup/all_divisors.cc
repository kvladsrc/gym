#include <algorithm>
#include <iostream>
#include <vector>

using std::sort;
using std::vector;

namespace {

vector<int> all_divisors(int n) {
  vector<int> res;

  // EDGE_CASE: divisors of negative numbers.
  n = abs(n);

  for (int d = 1; d * d <= n; ++d) {
    if (n % d == 0) {
      res.push_back(d);
      auto s = n / d;
      if (d != s) {
        res.push_back(s);
      }
    }
  }

  return res;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  auto divs = all_divisors(n);
  sort(divs.begin(), divs.end());

  for (auto d : divs) {
    std::cout << d << " ";
  }
  std::cout << "\n";

  return 0;
}

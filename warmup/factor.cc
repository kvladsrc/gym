#include <iostream>
#include <vector>

using std::vector;

namespace {

vector<int> factor(int n) {
  vector<int> res;

  // EDGE_CASE: divisors of negative numbers.
  n = abs(n);

  // Invariant: n % i != 0 for every i, 2 <= i < d.
  for (int d = 2; d * d <= n; ++d) {
    while (n % d == 0) {
      res.push_back(d);
      n /= d;
    }
  }

  if (n > 1) {
    res.push_back(n);
  }

  return res;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  auto prime_divs = factor(n);
  for (auto d : prime_divs) {
    std::cout << d << " ";
  }
  std::cout << "\n";

  return 0;
}

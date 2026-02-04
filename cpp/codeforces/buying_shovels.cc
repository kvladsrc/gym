#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;

  while (t--) {
    int64_t n, k;
    std::cin >> n >> k;

    if (k >= n) {
      std::cout << 1 << "\n";
      continue;
    }

    int64_t res = n;
    for (int64_t i = 2; i * i <= n; ++i) {
      if (n % i == 0) {
        if (i <= k) {
          res = min(res, n / i);
        }

        if (n / i <= k) {
          res = min(i, res);
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}

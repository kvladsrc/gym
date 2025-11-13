#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

bool is_prime(int n) {
  // EDGE_CASE: Numbers smaller or equal to 1 is not primes by
  // definition.
  if (n <= 1) {
    return false;
  }

  for (int d = 2; d * d <= n; ++d) {
    if (n % d == 0) {
      return false;
    }
  }

  return true;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int d = 0;
    std::cin >> d;

    int res = 1;
    int last = 1;

    for (int j = 0; j < 2; ++j) {
      for (int i = last + d;; ++i) {
        if (is_prime(i)) {
          res *= i;
          last = i;
          break;
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}

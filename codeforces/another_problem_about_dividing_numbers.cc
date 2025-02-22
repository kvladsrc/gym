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
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int a = 0;
    int b = 0;
    int k = 0;
    std::cin >> a >> b >> k;

    /*
     * In two operations we can make every to number equal to 1. So
     * problem to find if there are enought divisors to make k - 2
     * operations and end up with a != 1 and b != 1.
     */

    if (k == 1) {
      if (a % b == 0 || b % a == 0) {
        if (a == b) {
          std::cout << "NO" << "\n";
        } else {
          std::cout << "YES" << "\n";
        }
      } else {
        std::cout << "NO" << "\n";
      }
      continue;
    }

    int factor_a = 0;
    for (int i = 2; i * i <= a; ++i) {
      while (a % i == 0) {
        factor_a++;
        a /= i;
      }
    }

    if (a > 1) {
      factor_a++;
    }

    int factor_b = 0;
    for (int i = 2; i * i <= b; ++i) {
      while (b % i == 0) {
        factor_b++;
        b /= i;
      }
    }

    if (b > 1) {
      factor_b++;
    }

    if ((factor_a + factor_b) >= k) {
      std::cout << "YES" << "\n";
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}

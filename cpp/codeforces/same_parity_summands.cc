#include <algorithm>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;

  while (t--) {
    int n, k;
    std::cin >> n >> k;

    if (n < k || (k % 2 == 0 && n % 2) || (k % 2 && n % 2 == 0 && n < 2 * k)) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    std::cout << "YES"
              << "\n";
    bool even = (n % 2 == 0 && k % 2) ? true : false;
    while (k > 1) {
      if (even) {
        std::cout << 2 << " ";
        n -= 2;
        k--;
      } else {
        std::cout << 1 << " ";
        n--;
        k--;
      }
    }
    std::cout << n << "\n";
  }

  return 0;
}

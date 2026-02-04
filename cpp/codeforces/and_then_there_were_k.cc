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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    if (n == 1) {
      std::cout << 0 << "\n";
      continue;
    }

    // Finding k such that n & (n - 1) & (n - 2) & ... & k = 0
    int res = 1;
    while (res * 2 <= n) {
      res *= 2;
    }

    if (res > 1) {
      res--;
    }

    std::cout << res << "\n";
  }

  return 0;
}

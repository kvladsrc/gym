#include <algorithm>
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

int64_t solve(int64_t n) {
  int64_t res = 0;
  while (n > 1) {
    res += (n / 2) * (4 * (n - 1));
    n -= 2;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    std::cout << solve(n) << "\n";
  }
  return 0;
}

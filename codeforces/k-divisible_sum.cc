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

    if (k >= n) {
      std::cout << k / n + (k % n ? 1 : 0) << "\n";
      continue;
    }

    if (n % k == 0) {
      std::cout << 1 << "\n";
    } else {
      std::cout << 2 << "\n";
    }
  }

  return 0;
}

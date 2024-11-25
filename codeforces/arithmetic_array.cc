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
    int n;
    std::cin >> n;

    int total = 0;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      total += buf;
    }

    if (total > n) {
      std::cout << total - n << "\n";
      continue;
    }

    std::cout << (total == n ? 0 : 1) << "\n";
  }
  return 0;
}

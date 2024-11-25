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

    if (n == 2) {
      std::cout << -1 << "\n";
      continue;
    }

    for (int i = 1; i <= n; ++i) {
      for (int j = 1; j <= n; ++j) {
        if (j % 2 == 0) {
          std::cout << (i % n) * n + j << " ";
        } else {
          std::cout << (i - 1) * n + j << " ";
        }
      }
      std::cout << "\n";
    }
  }
  return 0;
}

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

    int prev = -1, prev_prev = -1;
    bool res = true;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;

      if (i > 1) {
        if (buf % 2 != prev_prev % 2) res = false;
      }

      prev_prev = prev;
      prev = buf;
    }

    if (res) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}

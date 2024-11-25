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
    for (int i = 0; i < 3; ++i) {
      bool a = false;
      bool b = false;
      bool c = false;

      for (int j = 0; j < 3; ++j) {
        char buf;
        std::cin >> buf;
        if (buf == 'A') a = true;
        if (buf == 'B') b = true;
        if (buf == 'C') c = true;
      }

      if (!a) std::cout << 'A' << "\n";
      if (!b) std::cout << 'B' << "\n";
      if (!c) std::cout << 'C' << "\n";
    }
  }
  return 0;
}

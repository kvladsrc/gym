#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
    bool res = false;
    for (int i = 0; i < 3; ++i) {
      char c;
      std::cin >> c;
      if ((i == 0 && c == 'a') || (i == 1 && c == 'b') ||
          (i == 2 && c == 'c')) {
        res = true;
      }
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

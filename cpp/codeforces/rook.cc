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

  vector<char> x_axe(8);
  vector<char> y_axe(8);
  for (int i = 0; i < 8; ++i) {
    y_axe[i] = '1' + i;
    x_axe[i] = 'a' + i;
  }

  while (t--) {
    char x;
    std::cin >> x;
    char y;
    std::cin >> y;

    for (int i = 0; i < 8; ++i) {
      if (x != x_axe[i]) {
        std::cout << x_axe[i] << y << "\n";
      }

      if (y != y_axe[i]) {
        std::cout << x << y_axe[i] << "\n";
      }
    }
  }

  return 0;
}

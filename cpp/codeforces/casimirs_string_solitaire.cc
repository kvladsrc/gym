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
    string in;
    std::cin >> in;
    int ac = 0, bc = 0, cc = 0;
    for (char c : in) {
      if (c == 'A') ac++;
      if (c == 'B') bc++;
      if (c == 'C') cc++;
    }

    if (ac + cc == bc) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }
  return 0;
}

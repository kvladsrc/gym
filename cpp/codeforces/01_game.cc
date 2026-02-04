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
    string s;
    std::cin >> s;
    int ones = 0, zeroes = 0;
    for (char c : s) {
      if (c == '1') {
        ones++;
      } else {
        zeroes++;
      }
    }

    int turns = min(ones, zeroes);
    if (turns % 2) {
      std::cout << "DA"
                << "\n";
    } else {
      std::cout << "NET"
                << "\n";
    }
  }

  return 0;
}

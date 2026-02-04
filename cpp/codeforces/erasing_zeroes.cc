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

    int zeroes = 0, leading = -1;
    int zeroes_before_last = 0;
    for (char c : in) {
      if (c == '0') {
        zeroes++;
      } else {
        zeroes_before_last = zeroes;
        if (leading == -1) {
          leading = zeroes;
        }
      }
    }

    if (leading == -1) {
      std::cout << 0 << "\n";
    } else {
      int ending = zeroes - zeroes_before_last;
      std::cout << zeroes - leading - ending << "\n";
    }
  }

  return 0;
}

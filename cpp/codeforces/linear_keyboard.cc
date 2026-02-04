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
    unordered_map<char, int> pos;
    for (int i = 1; i <= 26; ++i) {
      char buf;
      std::cin >> buf;
      pos[buf] = i;
    }

    string in;
    std::cin >> in;

    int res = 0;
    int cur_pos = pos[in[0]];
    for (char c : in) {
      res += abs(cur_pos - pos[c]);
      cur_pos = pos[c];
    }
    std::cout << res << "\n";
  }
  return 0;
}

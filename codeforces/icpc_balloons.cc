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
    int n;
    std::cin >> n;
    unordered_map<char, bool> solved;
    int res = 0;
    while (n--) {
      char c;
      std::cin >> c;
      res++;
      if (!solved[c]) {
        res++;
        solved[c] = true;
      }
    }
    std::cout << res << "\n";
  }
  return 0;
}

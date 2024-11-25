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

string min_invert(string in) {
  string res;

  bool first = true;
  for (char c : in) {
    int num = c - '0';

    if (num > (9 - num)) {
      if (first && num == 9) {
        res += num + '0';
      } else {
        res += (9 - num) + '0';
      }
    } else {
      res += num + '0';
    }
    first = false;
  }

  return res;
}

int main(int argc, char *argv[]) {
  string in;
  std::cin >> in;
  std::cout << min_invert(in) << "\n";

  return 0;
}

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
  int n;
  std::cin >> n;
  string f, s;
  int f_c = 0, s_c = 0;
  while (n--) {
    string res;
    std::cin >> res;

    if (f.empty()) {
      f = res;
      f_c++;
      continue;
    }

    if (res == f) {
      f_c++;
      continue;
    }

    if (s.empty()) {
      s = res;
      s_c++;
      continue;
    }

    s_c++;
  }

  if (f_c > s_c) {
    std::cout << f << "\n";
  } else {
    std::cout << s << "\n";
  }

  return 0;
}

#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <iterator>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

vector<int> decode(string &in) {
  vector<int> res;
  bool inside = false;
  for (char c : in) {
    if (c == '.' && !inside) {
      res.push_back(0);
      continue;
    }

    if (c == '.' && inside) {
      inside = false;
      res.push_back(1);
      continue;
    }

    if (c == '-' && inside) {
      inside = false;
      res.push_back(2);
      continue;
    }

    inside = true;
  }

  return res;
}

int main(int argc, char *argv[]) {
  string in;
  std::cin >> in;

  auto d = decode(in);
  for (int i : d) {
    std::cout << i;
  }

  return 0;
}

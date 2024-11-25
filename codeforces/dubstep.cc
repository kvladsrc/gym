#include <algorithm>
#include <cstddef>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

void remove_extra_spaces(string &s) {
  int pre = true;
  auto it = s.begin();
  while (it != s.end()) {
    if (*it == ' ') {
      if (pre) {
        it = s.erase(it);
        continue;
      }

      pre = true;
      ++it;
      continue;
    }

    pre = false;
    ++it;
  }
}

string orig(string mix) {
  string wub("WUB");
  string buf;
  string res;
  size_t cur = 0;
  for (char c : mix) {
    if (c == wub[cur]) {
      buf += c;
      cur++;
      if (cur == wub.size()) {
        cur = 0;
        res += ' ';
        buf.clear();
      }
    } else {
      res += buf;
      buf.clear();
      cur = 0;
      if (c == wub[cur]) {
        buf += c;
        cur++;
      } else {
        res += c;
      }
    }
  }
  res += buf;

  remove_extra_spaces(res);
  return res;
}

int main(int argc, char *argv[]) {
  string mix;
  std::cin >> mix;
  std::cout << orig(mix) << "\n";

  return 0;
}

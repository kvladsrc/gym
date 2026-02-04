#ifndef CPP_WARMUP_BOYER_MOORE_H_
#define CPP_WARMUP_BOYER_MOORE_H_

#include <map>
#include <string>

namespace warmup {

void fill_last_seen(const std::string& pattern,
                    std::map<char, std::size_t>& last_seen);
bool boyer_moore(const std::string& s, const std::string& pattern);

}  // namespace warmup

#endif  // CPP_WARMUP_BOYER_MOORE_H_
